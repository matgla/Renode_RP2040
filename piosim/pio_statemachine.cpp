/**
 * pio_statemachine.cpp
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

#include "pio_statemachine.hpp"

#include <bit>
#include <chrono>
#include <format>
#include <iostream>
#include <thread>

#include "decoder.hpp"
#include "gpio.hpp"
#include "renode_log.hpp"

namespace piosim
{

namespace
{

inline uint32_t rotate_left(uint32_t data, uint32_t pin_base, uint32_t pin_count)
{
  pin_base %= 32;
  const uint32_t mask = ((1ul << pin_count) - 1) << pin_base;
  return (std::rotl(data, pin_base) & mask);
}

inline uint32_t rotate_right(uint32_t data, uint32_t pin_base, uint32_t pin_count)
{
  pin_base %= 32;
  const uint32_t mask = (1ul << pin_count) - 1;
  return (std::rotr(data, pin_base) & mask);
}

inline uint32_t bitreverse(uint32_t data)
{
  uint32_t o = 0;
  for (int i = 0; i < 32; ++i)
  {
    if ((data & (1 << i)))
    {
      o |= static_cast<uint32_t>(1 << (31 - i));
    }
  }
  return o;
}

} // namespace

PioStatemachine::PioStatemachine(int id, std::span<const uint16_t> program,
                                 std::span<bool> irqs, IOSync &io_sync)
  : id_{id}
  , running_{false}
  , stop_{false}
  , enabled_{false}
  , clock_divider_{1}
  , stalled_{false}
  , sideset_done_{false}
  , ignore_delay_{false}
  , request_pause_{false}
  , program_counter_{0}
  , immediate_instruction_{std::nullopt}
  , wait_for_irq_{std::nullopt}
  , program_{program}
  , irqs_{irqs}
  , mutex_{}
  , cv_{}
  , thread_{}
  , scheduleSteps_{0}
  , x_{0}
  , y_{0}
  , osr_{0}
  , isr_{0}
  , osr_counter_{32}
  , isr_counter_{0}
  , delay_counter_{0}
  , delay_{0}
  , tx_{}
  , rx_{}
  , clock_divider_register_{.reg =
                              {
                                ._r = 0,
                                .frac = 0,
                                .integral = 1,
                              }}
  , exec_control_register_{.reg =
                             {
                               .status_n = 0,
                               .status_sel = 0,
                               ._reserved = 0,
                               .wrap_bottom = 0,
                               .wrap_top = 0x1f,
                               .out_sticky = 0,
                               .inline_out_en = 0,
                               .out_en_sel = 0,
                               .jump_pin = 0,
                               .side_pindir = 0,
                               .side_en = 0,
                               .exec_stalled = 0,
                             }}
  , shift_control_register_{.reg =
                              {
                                ._reserved = 0,
                                .autopush = 0,
                                .autopull = 0,
                                .in_shiftdir = 1,
                                .out_shiftdir = 1,
                                .push_threshold = 0,
                                .pull_threshold = 0,
                                .fjoin_tx = 0,
                                .fjoin_rx = 0,
                              }}
  , pin_control_register_{.reg = {.out_base = 0,
                                  .set_base = 0,
                                  .sideset_base = 0,
                                  .in_base = 0,
                                  .out_count = 0,
                                  .set_count = 5,
                                  .sideset_count = 0}}
  , io_sync_{io_sync}
  , request_io_{false}
{
  // thread_ = std::thread(&PioStatemachine::loop, this);
}

PioStatemachine::~PioStatemachine()
{
  // std::unique_lock lk(mutex_);
  // stop_ = true;
  // enabled_ = false;
  // lk.unlock();
  // cv_.notify_one();
  // thread_.join();
}

void PioStatemachine::enable(bool enable)
{
  if (enable && !enabled_)
  {
    renode_log(LogLevel::Debug, std::format("enabling SM{}", id_));
    // std::unique_lock lk(mutex_);
    enabled_ = true;
    //  cv_.notify_one();
  }
  else if (!enable && enabled_)
  {
    //   std::unique_lock lk(mutex_);
    enabled_ = false;
    //    cv_.notify_one();
  }
}

void PioStatemachine::restart()
{
}

void PioStatemachine::clock_divider_restart()
{
}

bool PioStatemachine::process_delay()
{
  if (stalled_)
  {
    return true;
  }

  ++delay_counter_;
  if (delay_counter_ > delay_)
  {
    return true;
  }
  return false;
}

void PioStatemachine::schedule_delay(uint16_t data)
{
  const int delay_bits = (5 - pin_control_register_.reg.sideset_count);
  const int delay_mask = (1 << delay_bits) - 1;
  const int delay = (data & delay_mask);
  if (delay != 0)
  {
    delay_counter_ = 0;
    delay_ = delay;
  }
}

void PioStatemachine::increment_program_counter()
{
  if (immediate_instruction_)
  {
    return;
  }
  if (program_counter_ == exec_control_register_.reg.wrap_top)
  {
    program_counter_ = exec_control_register_.reg.wrap_bottom;
  }
  else
  {
    ++program_counter_;
  }
}

void PioStatemachine::process_sideset(uint16_t data)
{
  const int delay_bits = (5 - pin_control_register_.reg.sideset_count);

  if (!sideset_done_)
  {
    if (pin_control_register_.reg.sideset_count > 0)
    {
      const uint32_t sideset_mask =
        (1u << pin_control_register_.reg.sideset_count) - 1;
      const uint32_t sideset = (data >> delay_bits) & sideset_mask;
      const uint32_t gpio_bitset =
        rotate_left(sideset, pin_control_register_.reg.sideset_base,
                    pin_control_register_.reg.sideset_count);
      const uint32_t gpio_bitmap =
        rotate_left((1u << pin_control_register_.reg.sideset_count) - 1,
                    pin_control_register_.reg.sideset_base, 32);

      bool enabled = true;
      if (exec_control_register_.reg.side_en)
      {
        if (!(data & (1 << 5)))
        {
          enabled = false;
        }
      }
      if (enabled)
      {

        if (exec_control_register_.reg.side_pindir)
        {
          //         io_sync_.schedule_action([gpio_bitset, gpio_bitmap] {
          renode_gpio_set_pindir_bitset(gpio_bitset, gpio_bitmap);
          request_io_ = true;
          //        });
        }
        else
        {
          //       io_sync_.schedule_action([gpio_bitset, gpio_bitmap] {
          renode_gpio_set_pin_bitset(gpio_bitset, gpio_bitmap);
          request_io_ = true;
          //      });
        }
      }
    }
    sideset_done_ = true;
  }
}

bool PioStatemachine::jump_condition(uint8_t condition)
{
  switch (condition)
  {
  case 0:
    return true;
  case 1:
    return x_ == 0;
  case 2:
    return x_-- != 0;
  case 3:
    return y_ == 0;
  case 4:
    return y_-- != 0;
  case 5:
    return x_ != y_;
  case 6:
    return renode_gpio_get_pin_state(exec_control_register_.reg.jump_pin);
  case 7:
    return osr_counter_ < shift_control_register_.reg.pull_threshold;
  default:
    return true;
  }
}

bool PioStatemachine::process_jump(uint16_t data)
{
  const uint8_t condition = static_cast<uint8_t>((data >> 5) & 0x7);
  const uint8_t address = static_cast<uint8_t>(data & 0x1f);

  // renode_log(LogLevel::Error,
  //            std::format("Jump: {} {} {}", condition, address, program_counter_));

  if (jump_condition(condition))
  {
    program_counter_ = address;
    return true;
  }
  else
  {
    increment_program_counter();
    return true;
  }
}

bool PioStatemachine::process_wait(uint16_t data)
{
  const bool polarity = ((1 << 7) & data) != 0;
  const uint8_t source = static_cast<uint8_t>((data >> 5) & 0x3);
  const uint8_t index = static_cast<uint8_t>(data & 0x1f);
  renode_log(LogLevel::Error, std::format("ID: {}, waiting: {}", id_, data));
  switch (source)
  {
  case 0: {
    bool pin_state = renode_gpio_get_pin_state(index);
    return pin_state == polarity;
  }
  case 1: {
    const int pin_index = ((index + pin_control_register_.reg.in_base) % 32);
    const bool pin_state = renode_gpio_get_pin_state(index);

    renode_log(LogLevel::Error,
               std::format("Wait for pin: {}", pin_index, ", state: {}", pin_state));
    return pin_state == polarity;
  }
  case 2: {
    int id = index;
    if ((index & (1 << 4)) != 0)
    {
      id = (id_ + index) % 4;
    }

    if (irqs_[id])
    {
      if (polarity)
      {
        irqs_[id] = false;
      }
      return true;
    }
    return false;
  }
  }
  return true;
}

void PioStatemachine::push_isr()
{
  rx_.push(isr_);
  isr_ = 0;
  isr_counter_ = 0;
}

bool PioStatemachine::process_push(uint16_t data)
{
  const bool if_full = (data & (1 << 6)) != 0;
  const bool block = (data & (1 << 5)) != 0;

  if (if_full)
  {
    if (isr_counter_ >= shift_control_register_.reg.push_threshold)
    {
      increment_program_counter();
      return true;
    }
  }

  if (rx_.full())
  {
    if (block)
    {
      return false;
    }
  }
  else
  {
    push_isr();
  }
  increment_program_counter();
  return true;
}

void PioStatemachine::load_osr(uint32_t value)
{
  osr_ = value;
  osr_counter_ = 0;
}

void PioStatemachine::load_osr()
{
  load_osr(tx_.pop());
}

bool PioStatemachine::process_pull(uint16_t data)
{
  const bool if_empty = (data & (1 << 6)) != 0;
  const bool block = (data & (1 << 5)) != 0;

  if (if_empty)
  {
    if (osr_counter_ >= shift_control_register_.reg.pull_threshold)
    {
      increment_program_counter();
      return true;
    }
  }

  if (tx_.empty())
  {
    if (block)
    {
      return false;
    }
    load_osr(x_);
  }
  else
  {
    load_osr();
  }
  increment_program_counter();
  return true;
}

bool PioStatemachine::process_pushpull(uint16_t data)
{
  const bool is_push = (data & (1 << 7)) == 0;
  if (is_push)
  {
    return PioStatemachine::process_push(data);
  }
  else
  {
    return PioStatemachine::process_pull(data);
  }
}

void PioStatemachine::write_isr(uint32_t bits, uint32_t data)
{
  if (shift_control_register_.reg.in_shiftdir == 0)
  {
    isr_ = (isr_ << bits) | data;
  }
  else
  {
    isr_ = (isr_ >> bits) | (data << (32 - bits));
  }
  isr_counter_ += bits;

  if (shift_control_register_.reg.autopush &&
      (isr_counter_ >= shift_control_register_.reg.push_threshold))
  {
    push_isr();
  }
}

bool PioStatemachine::process_in(uint16_t data)
{
  const uint8_t source = static_cast<uint8_t>((data >> 5) & 0x7);
  uint8_t bit_count = static_cast<uint8_t>(data & 0x1f);
  if (bit_count == 0)
  {
    bit_count = 32;
  }

  uint isr_data = 0;
  switch (source)
  {
  case 0: {
    isr_data = renode_gpio_get_pin_bitmap();
    isr_data = rotate_right(isr_, pin_control_register_.reg.in_base, 32);
    break;
  }
  case 1: {
    isr_data = x_;
    break;
  }
  case 2: {
    isr_data = y_;
    break;
  }
  case 6: {
    isr_data = isr_;
    break;
  }
  case 7: {
    isr_data = osr_;
    break;
  }
  default: {
    isr_data = 0;
    break;
  }
  }

  write_isr(bit_count, isr_data);
  increment_program_counter();
  return true;
}

uint32_t PioStatemachine::read_osr(uint32_t bits)
{
  uint32_t data = 0;
  const uint32_t mask = static_cast<uint32_t>((1ul << bits) - 1);

  if (shift_control_register_.reg.out_shiftdir == 0)
  {
    data = (osr_ >> (32 - bits)) & mask;
    osr_ <<= bits;
  }
  else
  {
    data = osr_ & mask;
    osr_ >>= bits;
  }

  osr_counter_ += bits;

  if (shift_control_register_.reg.autopull &&
      (osr_counter_ >= shift_control_register_.reg.pull_threshold))
  {
    load_osr();
  }
  return data;
}

bool PioStatemachine::process_out(uint16_t data)
{
  const uint8_t source = static_cast<uint8_t>((data >> 5) & 0x7);
  uint8_t bit_count = static_cast<uint8_t>(data & 0x1f);
  if (bit_count == 0)
  {
    bit_count = 32;
  }

  uint osr_data = read_osr(bit_count);

  renode_log(LogLevel::Error,
             std::format("Processing out: {} {}", source, bit_count));

  switch (source)
  {
  case 0: {
    const uint32_t pins = rotate_left(osr_data, pin_control_register_.reg.out_base,
                                      pin_control_register_.reg.out_count);
    const uint32_t mask =
      rotate_left((1u << pin_control_register_.reg.out_count) - 1,
                  pin_control_register_.reg.out_base, 32);
    // io_sync_.schedule_action([pins, mask] {
    renode_gpio_set_pin_bitset(pins, mask);
    request_io_ = true;
    // });
    break;
  }
  case 1: {
    x_ = osr_data;
    break;
  }
  case 2: {
    y_ = osr_data;
    break;
  }
  case 3: {
    break;
  }
  case 4: {
    const uint32_t pins = rotate_left(osr_data, pin_control_register_.reg.out_base,
                                      pin_control_register_.reg.out_count);
    const uint32_t mask =
      rotate_left((1u << pin_control_register_.reg.out_count) - 1,
                  pin_control_register_.reg.out_base, 32);

    // io_sync_.schedule_action([pins, mask] {
    renode_gpio_set_pindir_bitset(pins, mask);
    request_io_ = true;
    // });

    break;
  }
  case 5: {
    program_counter_ = static_cast<uint16_t>(osr_data);
    return true;
  }
  case 6: {
    isr_ = osr_data;
    isr_counter_ = bit_count;
    break;
  }
  case 7: {
    immediate_instruction_ = static_cast<uint16_t>(osr_data);
    ignore_delay_ = true;
    return true;
  }
  default: {
    break;
  }
  }
  increment_program_counter();

  return true;
}

uint32_t PioStatemachine::get_from_source(uint32_t source)
{
  switch (source)
  {
  case 0:
    return rotate_right(renode_gpio_get_pin_bitmap(),
                        pin_control_register_.reg.in_base,
                        32); // pins not supported yet
  case 1:
    return x_;
  case 2:
    return y_;
  case 3:
    return 0;
  case 4:
    return 0;
  case 5: {
    uint data = 0;
    if (!exec_control_register_.reg.status_sel)
    {
      if (tx_.size() < exec_control_register_.reg.status_n)
      {
        data = ~data;
      }
    }
    else
    {
      if (rx_.size() < exec_control_register_.reg.status_n)
      {
        data = ~data;
      }
    }
    return data;
  }
  case 6:
    return isr_;
  case 7:
    return osr_;
  }
  return 0;
}

bool PioStatemachine::process_mov(uint16_t immediateData)
{
  const uint16_t destination = static_cast<uint16_t>((immediateData >> 5) & 0x7);
  const uint16_t source = static_cast<uint16_t>(immediateData & 0x7);
  const uint16_t operation = static_cast<uint16_t>((immediateData >> 3) & 0x03);

  uint32_t data = get_from_source(source);

  if (operation == 1)
  {
    data = ~data;
  }
  else if (operation == 2)
  {
    data = bitreverse(data);
  }

  switch (destination)
  {
  case 0: {
    const uint32_t mask = rotate_left((1u << pin_control_register_.reg.out_count),
                                      pin_control_register_.reg.out_base, 32);
    const uint32_t state = rotate_left(data, pin_control_register_.reg.out_base,
                                       pin_control_register_.reg.out_count);

    // io_sync_.schedule_action([state, mask] {
    renode_gpio_set_pin_bitset(state, mask);
    request_io_ = true;
    // });
    break;
  }
  case 1: {
    x_ = data;
    break;
  }
  case 2: {
    y_ = data;
    break;
  }
  case 3: {
    break;
  }
  case 4: {
    immediate_instruction_ = static_cast<uint16_t>(data);
    ignore_delay_ = true;
    return true;
  }
  case 5: {
    program_counter_ = static_cast<uint16_t>(data);
    return true;
  }
  case 6: {
    isr_ = data;
    isr_counter_ = 0;
    break;
  }
  case 7: {
    osr_ = data;
    osr_counter_ = 0;
    break;
  }
  }
  increment_program_counter();

  return true;
}

bool PioStatemachine::process_irq(uint16_t data)
{
  if (wait_for_irq_)
  {
    if (irqs_[*wait_for_irq_] == false)
    {
      increment_program_counter();
      return true;
    }
    return false;
  }

  const bool clear = (data & (1 << 6)) != 0;
  const bool wait = (data & (1 << 5)) != 0;
  const uint8_t index = static_cast<uint8_t>(data & 0x1f);

  int id = index;
  if ((id & (1 << 4)) != 0)
  {
    id = (id_ + index) % 4;
  }

  if (clear)
  {
    irqs_[id] = false;
    increment_program_counter();
    return true;
  }
  else
  {
    irqs_[id] = true;
    if (wait)
    {
      wait_for_irq_ = id;
    }
  }

  return true;
}

bool PioStatemachine::process_set(uint16_t immediateData)
{
  const uint8_t destination = static_cast<uint8_t>((immediateData >> 5) & 0x7);
  const uint8_t data = static_cast<uint8_t>(immediateData & 0x1f);

  switch (destination)
  {
  case 0: {
    const uint32_t mask =
      rotate_left((1u << pin_control_register_.reg.set_count) - 1,
                  pin_control_register_.reg.set_base, 32);
    const uint32_t state = rotate_left(data, pin_control_register_.reg.set_base,
                                       pin_control_register_.reg.set_count);

    // io_sync_.schedule_action([mask, state] {
    renode_gpio_set_pin_bitset(state, mask);
    request_io_ = true;
    // });
    break;
  }
  case 1: {
    x_ = data;
    break;
  }
  case 2: {
    y_ = data;
    break;
  }
  case 4: {
    const uint32_t mask =
      rotate_left((1u << pin_control_register_.reg.set_count) - 1,
                  pin_control_register_.reg.set_base, 32);
    const uint32_t state = rotate_left(data, pin_control_register_.reg.set_base,
                                       pin_control_register_.reg.set_count);

    // io_sync_.schedule_action([state, mask] {
    renode_gpio_set_pindir_bitset(state, mask);
    request_io_ = true;
    // });
    break;
  }
  }
  increment_program_counter();
  return true;
}

bool PioStatemachine::step()
{
  request_io_ = false;
  if (!process_delay())
  {
    return true;
  }

  const uint16_t instruction =
    immediate_instruction_ ? *immediate_instruction_ : program_[program_counter_];

  const PioDecodedInstruction cmd{instruction};
  process_sideset(cmd.delay_or_sideset);

  bool finished = false;

  switch (cmd.opcode)
  {
  case PioDecodedInstruction::OpCode::Jmp: {
    finished = process_jump(cmd.immediate_data);
    break;
  }
  case PioDecodedInstruction::OpCode::Wait: {
    finished = process_wait(cmd.immediate_data);
    break;
  }
  case PioDecodedInstruction::OpCode::In: {
    finished = process_in(cmd.immediate_data);
    break;
  }

  case PioDecodedInstruction::OpCode::Out: {
    finished = process_out(cmd.immediate_data);
    break;
  }
  case PioDecodedInstruction::OpCode::PushPull: {
    finished = process_pushpull(cmd.immediate_data);
    break;
  }

  case PioDecodedInstruction::OpCode::Mov: {
    finished = process_mov(cmd.immediate_data);
    break;
  }
  case PioDecodedInstruction::OpCode::Irq: {
    finished = process_irq(cmd.immediate_data);
    break;
  }
  case PioDecodedInstruction::OpCode::Set: {
    finished = process_set(cmd.immediate_data);
    break;
  }
  }

  if (finished)
  {
    if (!ignore_delay_)
    {
      schedule_delay(cmd.delay_or_sideset);
    }
    ignore_delay_ = false;
    sideset_done_ = false;
    stalled_ = false;
    immediate_instruction_ = std::nullopt;
  }
  else
  {
    stalled_ = true;
  }

  if (request_io_)
  {
    return false;
  }

  return true;
}

void PioStatemachine::execute(uint32_t steps)
{
  // std::unique_lock lk(mutex_);
  if (!enabled_ && !immediate_instruction_)
  {
    return;
  }

  scheduleSteps_ += steps;
  if (steps > 0)
  {
    running_ = true;
  }
  // lk.unlock();
  cv_.notify_one();
}

bool PioStatemachine::done() const
{
  return (!running_ || !enabled_) && !(immediate_instruction_ && scheduleSteps_ > 0);
}

const Fifo &PioStatemachine::tx_fifo() const
{
  return tx_;
}

const Fifo &PioStatemachine::rx_fifo() const
{
  return rx_;
}

void PioStatemachine::push_tx(uint32_t data)
{
  // pause();
  tx_.push(data);
  // resume();
}

uint32_t PioStatemachine::pop_rx()
{
  // pause();
  uint32_t r = rx_.pop();
  // resume();
  return r;
}

uint32_t PioStatemachine::clock_divider_register() const
{
  return clock_divider_register_.value;
}

void PioStatemachine::clock_divider_register(uint32_t data)
{
  clock_divider_register_.value = data;
  double integral = static_cast<double>(clock_divider_register_.reg.integral);
  if (clock_divider_register_.reg.integral == 0)
  {
    integral = 65536;
  }
  double frac = clock_divider_register_.reg.frac;
  clock_divider_ = integral + (frac / 256);

  renode_log(LogLevel::Debug, std::format("SM{}: changed clock divider to: {}", id_,
                                          clock_divider_.load()));
}

uint32_t PioStatemachine::exec_control_register() const
{
  return exec_control_register_.value;
}

void PioStatemachine::exec_control_register(uint32_t data)
{
  // pause();
  exec_control_register_.value = (data & 0x7fffffff);
  // resume();
}

uint32_t PioStatemachine::shift_control_register() const
{
  return shift_control_register_.value;
}

void PioStatemachine::shift_control_register(uint32_t data)
{
  // pause();
  shift_control_register_.value = data;
  // resume();
}

uint32_t PioStatemachine::pin_control_register() const
{
  return pin_control_register_.value;
}

void PioStatemachine::pin_control_register(uint32_t data)
{
  // pause();
  pin_control_register_.value = data;
  // resume();
}

uint8_t PioStatemachine::program_counter() const
{
  return program_counter_;
}

uint16_t PioStatemachine::current_instruction() const
{
  if (immediate_instruction_)
  {
    return *immediate_instruction_;
  }
  return program_[program_counter_];
}

void PioStatemachine::execute_immediately(uint16_t instruction)
{
  // {
  // std::scoped_lock lk(mutex_);
  immediate_instruction_ = instruction;
  // }
}

void PioStatemachine::loop()
{
  // std::unique_lock<std::mutex> lk(mutex_);
  // while (!stop_)
  // {
  //   cv_.wait(lk, [this] {
  //     return scheduleSteps_ > 0 || stop_;
  //   });

  //   if (stop_)
  //   {
  //     return;
  //   }

  //   for (uint32_t i = 0; i < scheduleSteps_; ++i)
  //   {
  //     if (request_pause_)
  //     {
  //       running_ = false;
  //       lk.unlock();
  //       cv_.notify_one();
  //       lk.lock();
  //       if (!cv_.wait_for(lk, std::chrono::milliseconds(100), [this] {
  //             return !request_pause_;
  //           }))
  //       {
  //         std::cerr << "Pause timeout" << std::endl;
  //       }
  //     }
  //     step();
  //   }

  //   scheduleSteps_ = 0;
  //   running_ = false;

  //   std::unique_lock iolk(io_sync_.mutex);
  //   io_sync_.sync = true;
  //   io_sync_.cv.notify_all();
  // }
}

void PioStatemachine::pause()
{
  // std::unique_lock<std::mutex> lk(mutex_);
  // request_pause_ = true;
  // if (!cv_.wait_for(lk, std::chrono::milliseconds(100), [this] {
  //       return !running_;
  //     }))
  // {
  //   std::cerr << "Wait for pause timeouted" << std::endl;
  // }
}

void PioStatemachine::resume()
{
  // std::unique_lock lk(mutex_);
  // request_pause_ = false;
  // lk.unlock();
  // cv_.notify_one();
}

} // namespace piosim
