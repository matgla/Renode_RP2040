/**
 * pio_statemachine.cpp
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

#include "pio_statemachine.hpp"

#include <bit>
#include <format>

#include "gpio.hpp"
#include "opcode.hpp"
#include "renode_log.hpp"

namespace piosim
{

namespace
{

inline uint32_t __attribute__((always_inline)) rotate_left(uint32_t data,
                                                           uint32_t pin_base,
                                                           uint32_t pin_count)
{
  pin_base %= 32;
  const uint32_t mask = ((1ul << pin_count) - 1) << pin_base;
  return (std::rotl(data, pin_base) & mask);
}

inline uint32_t __attribute__((always_inline)) rotate_right(uint32_t data,
                                                            uint32_t pin_base,
                                                            uint32_t pin_count)
{
  pin_base %= 32;
  const uint32_t mask = (1ul << pin_count) - 1;
  return (std::rotr(data, pin_base) & mask);
}

inline uint32_t __attribute__((always_inline)) bitreverse(uint32_t data)
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
                                 std::span<bool> irqs)
  : id_{id}
  , enabled_{false}
  , clock_divider_{1}
  , stalled_{false}
  , sideset_done_{false}
  , ignore_delay_{false}
  , program_counter_{0}
  , wait_for_irq_{std::nullopt}
  , program_{program}
  , irqs_{irqs}
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
  , clock_divider_register_{.frac = 0, .integral = 1}
  , exec_control_register_{.status_n = 0,
                           .status_sel = 0,
                           .wrap_bottom = 0,
                           .wrap_top = 0x1f,
                           .out_sticky = 0,
                           .inline_out_en = 0,
                           .out_en_sel = 0,
                           .jump_pin = 0,
                           .side_pindir = 0,
                           .side_en = 0,
                           .exec_stalled = 0}
  , shift_control_register_{.autopush = 0,
                            .autopull = 0,
                            .in_shiftdir = 1,
                            .out_shiftdir = 1,
                            .push_threshold = 32,
                            .pull_threshold = 32,
                            .fjoin_tx = 0,
                            .fjoin_rx = 0}
  , pin_control_register_{.out_base = 0,
                          .set_base = 0,
                          .sideset_base = 0,
                          .in_base = 0,
                          .out_count = 0,
                          .set_count = 5,
                          .sideset_count = 0}
{
  restart();
}

PioStatemachine::~PioStatemachine()
{
}

void __attribute__((always_inline)) PioStatemachine::log(
  LogLevel level, const std::string_view &message) const
{
  renode_log(level, std::format("SM{}: {}", id_, message));
}

void PioStatemachine::enable(bool enable)
{
  if (enable == enabled_)
  {
    return;
  }
  log(LogLevel::Debug, std::format(" enabling -> {}", enable));
  enabled_ = enable;
}

void PioStatemachine::restart()
{
  stalled_ = false;
  wait_for_irq_ = std::nullopt;
  osr_ = 0;
  isr_ = 0;
  osr_counter_ = 32;
  isr_counter_ = 0;
  delay_counter_ = 0;
  delay_ = 0;
}

void PioStatemachine::clock_divider_restart()
{
}

bool __attribute__((always_inline)) PioStatemachine::process_delay()
{
  return stalled_ == true ? true : ++delay_counter_ > delay_;
}

void PioStatemachine::schedule_delay(uint16_t data)
{
  const int delay_bits = (5 - pin_control_register_.sideset_count);
  const int delay_mask = (1 << delay_bits) - 1;
  const int delay = (data & delay_mask);
  if (delay != 0)
  {
    delay_counter_ = 1;
    delay_ = delay;
  }
}

void __attribute__((always_inline)) PioStatemachine::increment_program_counter()
{
  if (program_counter_ == exec_control_register_.wrap_top)
  {
    program_counter_ = exec_control_register_.wrap_bottom;
  }
  else
  {
    ++program_counter_;
  }
}

void __attribute__((always_inline)) PioStatemachine::process_sideset(uint16_t data)
{
  const int delay_bits = (5 - pin_control_register_.sideset_count);

  if (!sideset_done_)
  {
    if (pin_control_register_.sideset_count > 0)
    {
      const uint32_t sideset_bits = exec_control_register_.side_en
                                      ? pin_control_register_.sideset_count - 1
                                      : pin_control_register_.sideset_count;
      const uint32_t sideset_mask = (1u << pin_control_register_.sideset_count) - 1;
      const uint32_t sideset = (data >> delay_bits) & sideset_mask;
      const uint32_t gpio_bitset =
        rotate_left(sideset, pin_control_register_.sideset_base,
                    pin_control_register_.sideset_count);
      const uint32_t gpio_bitmap = rotate_left(
        1u << (sideset_bits - 1), pin_control_register_.sideset_base, 32);

      bool enabled = true;
      if (exec_control_register_.side_en)
      {
        log(LogLevel::Error,
            std::format("Side register enabled: and is {}, dat: {:x}",
                        data & (1 << 4), data));
        if (!(data & (1 << 4)))
        {
          enabled = false;
        }
      }
      if (enabled)
      {
        log(LogLevel::Error,
            std::format("Sideset: {}, {}", gpio_bitset, gpio_bitmap));

        if (exec_control_register_.side_pindir)
        {
          renode_gpio_set_pindir_bitset(gpio_bitset, gpio_bitmap);
        }
        else
        {
          renode_gpio_set_pin_bitset(gpio_bitset, gpio_bitmap);
        }

        log(LogLevel::Error, "sideset done");
      }
    }
    sideset_done_ = true;
  }
}

inline bool PioStatemachine::jump_condition(uint8_t condition)
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
    return renode_gpio_get_pin_state(exec_control_register_.jump_pin);
  case 7:
    return osr_counter_ < shift_control_register_.pull_threshold;
  default:
    return true;
  }
}

bool __attribute__((always_inline)) PioStatemachine::process_jump(uint16_t data)
{
  const uint8_t condition = static_cast<uint8_t>((data >> 5) & 0x7);
  const uint8_t address = static_cast<uint8_t>(data & 0x1f);

  bool execute = true;
  switch (condition)
  {
  case 1:
    execute = x_ == 0;
    break;
  case 2:
    execute = x_-- != 0;
    break;
  case 3:
    execute = y_ == 0;
    break;
  case 4:
    execute = y_-- != 0;
    break;
  case 5:
    execute = x_ != y_;
    break;
  case 6:
    execute = renode_gpio_get_pin_state(exec_control_register_.jump_pin);
    break;
  case 7:
    execute = osr_counter_ < shift_control_register_.pull_threshold;
    break;
  }
  log(LogLevel::Error, std::format("Jump: {}, met: {}", condition, execute));

  if (execute)
  {
    program_counter_ = address;
    return true;
  }

  increment_program_counter();
  return true;
}

bool PioStatemachine::process_wait(uint16_t data)
{
  const bool polarity = ((1 << 7) & data) != 0;
  const uint8_t source = static_cast<uint8_t>((data >> 5) & 0x3);
  const uint8_t index = static_cast<uint8_t>(data & 0x1f);

  bool condition_met = false;
  switch (source)
  {
  case 0: {
    bool pin_state = renode_gpio_get_pin_state(index);
    condition_met = pin_state == polarity;
    break;
  }
  case 1: {
    const int pin_index = ((index + pin_control_register_.in_base) % 32);
    const bool pin_state = renode_gpio_get_pin_state(pin_index);
    condition_met = pin_state == polarity;
    log(LogLevel::Error,
        std::format("Reading pin: {}, met: {}", pin_index, condition_met));
    break;
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
      condition_met = true;
      break;
    }
    return false;
  }
  }
  if (condition_met)
  {
    increment_program_counter();
  }
  return true;
}

bool PioStatemachine::push_isr()
{
  if (rx_.full())
  {
    log(LogLevel::Error, std::format("RX FULL: {}", rx_.size()));
    return false;
  }
  log(LogLevel::Error, std::format("RX push: {}", isr_));
  rx_.push(isr_);
  isr_ = 0;
  isr_counter_ = 0;
  return true;
}

bool PioStatemachine::process_push(uint16_t data)
{
  const bool if_full = (data & (1 << 6)) != 0;
  const bool block = (data & (1 << 5)) != 0;

  if (if_full)
  {
    if (isr_counter_ >= shift_control_register_.push_threshold)
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

void __attribute__((always_inline)) PioStatemachine::load_osr(uint32_t value)
{
  osr_ = value;
  osr_counter_ = 0;
}

bool __attribute__((always_inline)) PioStatemachine::load_osr()
{
  if (!tx_.empty())
  {
    load_osr(tx_.pop());
    return false;
  }
  return true;
}

bool PioStatemachine::process_pull(uint16_t data)
{
  const bool if_empty = (data & (1 << 6)) != 0;
  const bool block = (data & (1 << 5)) != 0;

  if (if_empty)
  {
    if (osr_counter_ >= shift_control_register_.pull_threshold)
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

bool __attribute__((always_inline)) PioStatemachine::write_isr(uint32_t bits,
                                                               uint32_t data)
{
  if (shift_control_register_.in_shiftdir == 0)
  {
    isr_ = (isr_ << bits) | data;
  }
  else
  {
    isr_ = (isr_ >> bits) | (data << (32 - bits));
  }
  isr_counter_ += bits;

  if (shift_control_register_.autopush &&
      (isr_counter_ >= shift_control_register_.push_threshold))
  {
    return !push_isr();
  }

  return false;
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
    isr_data = rotate_right(isr_, pin_control_register_.in_base, 32);
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

  bool stalled = write_isr(bit_count, isr_data);
  if (stalled)
  {
    return false;
  }
  increment_program_counter();
  return true;
}

inline uint32_t __attribute__((always_inline)) PioStatemachine::read_osr(
  uint32_t bits)
{
  uint32_t data = 0;
  const uint32_t mask = static_cast<uint32_t>((1ul << bits) - 1);

  if (shift_control_register_.out_shiftdir == 0)
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

  if (shift_control_register_.autopull &&
      (osr_counter_ >= shift_control_register_.pull_threshold))
  {
    load_osr();
  }
  return data;
}

bool PioStatemachine::process_out(uint16_t data)
{
  const uint8_t source = (data >> 5) & 0x7;
  uint8_t bit_count = data & 0x1f;
  if (bit_count == 0)
  {
    bit_count = 32;
  }

  if (shift_control_register_.autopull &&
      osr_counter_ >= shift_control_register_.pull_threshold)
  {
    if (!load_osr())
    {
      return false;
    }
  }

  uint32_t osr_data = read_osr(bit_count);

  switch (source)
  {
  case 0: {
    const uint32_t pins = rotate_left(osr_data, pin_control_register_.out_base,
                                      pin_control_register_.out_count);
    const uint32_t mask = rotate_left((1u << pin_control_register_.out_count) - 1,
                                      pin_control_register_.out_base, 32);
    renode_gpio_set_pin_bitset(pins, mask);
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
    const uint32_t pins = rotate_left(osr_data, pin_control_register_.out_base,
                                      pin_control_register_.out_count);
    const uint32_t mask = rotate_left((1u << pin_control_register_.out_count) - 1,
                                      pin_control_register_.out_base, 32);

    renode_gpio_set_pindir_bitset(pins, mask);
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

uint32_t __attribute__((always_inline)) PioStatemachine::get_from_source(
  uint32_t source)
{
  switch (source)
  {
  case 0:
    return rotate_right(renode_gpio_get_pin_bitmap(), pin_control_register_.in_base,
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
    if (!exec_control_register_.status_sel)
    {
      if (tx_.size() < exec_control_register_.status_n)
      {
        data = ~data;
      }
    }
    else
    {
      if (rx_.size() < exec_control_register_.status_n)
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

  log(LogLevel::Error, std::format("MOV: {} to {}", source, destination));
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
    const uint32_t mask = rotate_left((1u << pin_control_register_.out_count),
                                      pin_control_register_.out_base, 32);
    const uint32_t state = rotate_left(data, pin_control_register_.out_base,
                                       pin_control_register_.out_count);

    renode_gpio_set_pin_bitset(state, mask);
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
  renode_log(LogLevel::Error, "IRQ");
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
    const uint32_t mask = rotate_left((1u << pin_control_register_.set_count) - 1,
                                      pin_control_register_.set_base, 32);
    const uint32_t state = rotate_left(data, pin_control_register_.set_base,
                                       pin_control_register_.set_count);

    renode_gpio_set_pin_bitset(state, mask);
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
    const uint32_t mask = rotate_left((1u << pin_control_register_.set_count) - 1,
                                      pin_control_register_.set_base, 32);
    const uint32_t state = rotate_left(data, pin_control_register_.set_base,
                                       pin_control_register_.set_count);

    renode_gpio_set_pindir_bitset(state, mask);
    break;
  }
  }
  increment_program_counter();
  return true;
}

bool PioStatemachine::step()
{
  if (!enabled_)
  {
    return true;
  }

  if (delay_counter_ < delay_)
  {
    if (id_ == 0)
      log(LogLevel::Error, std::format("delay: {}", delay_));
    ++delay_counter_;
    return true;
  }

  current_instruction_ = program_[program_counter_];
  return run_step();
}

bool __attribute__((always_inline)) PioStatemachine::run_step()
{
  static int i = 0;
  if (++i > 400)
  {
    return true;
  }

  const OpCode opcode = static_cast<OpCode>(current_instruction_ >> 13);
  const uint16_t delay_or_sideset = (current_instruction_ >> 8) & 0x1f;
  const uint16_t immediate_data = current_instruction_ & 0xff;
  if (id_ == 0)
  {
    log(LogLevel::Error,
        std::format("PC: {}, cmd: {}, delay: {:x}, immediate_data: {:x}, x: {:x}, "
                    "osr: {:x}, c: {}",
                    program_counter_, to_string(opcode), delay_or_sideset,
                    immediate_data, x_, osr_, osr_counter_));
  }
  process_sideset(delay_or_sideset);

  bool finished = false;

  switch (opcode)
  {
  case OpCode::Jmp: {
    finished = process_jump(immediate_data);
    break;
  }
  case OpCode::Wait: {
    finished = process_wait(immediate_data);
    break;
  }
  case OpCode::In: {
    finished = process_in(immediate_data);
    break;
  }

  case OpCode::Out: {
    finished = process_out(immediate_data);
    break;
  }
  case OpCode::PushPull: {
    finished = process_pushpull(immediate_data);
    break;
  }

  case OpCode::Mov: {
    finished = process_mov(immediate_data);
    break;
  }
  case OpCode::Irq: {
    finished = process_irq(immediate_data);
    break;
  }
  case OpCode::Set: {
    finished = process_set(immediate_data);
    break;
  }
  }

  if (finished)
  {
    if (!ignore_delay_)
    {
      const int delay_bits = (5 - pin_control_register_.sideset_count);
      const int delay_mask = (1 << delay_bits) - 1;
      const int delay = (delay_or_sideset & delay_mask);
      if (delay != 0)
      {
        delay_counter_ = 0;
        delay_ = delay;
      }
    }
    ignore_delay_ = false;
    sideset_done_ = false;
    stalled_ = false;

    return true;
  }

  stalled_ = true;
  return false;
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
  log(LogLevel::Error, std::format("Push to TX: {:x}", data));
  tx_.push(data);
}

uint32_t PioStatemachine::pop_rx()
{
  uint32_t r = rx_.pop();
  return r;
}

uint32_t PioStatemachine::clock_divider_register() const
{
  return Register<SMClockDivider>{.reg =
                                    {
                                      ._r = 0,
                                      .frac = clock_divider_register_.frac,
                                      .integral = clock_divider_register_.integral,
                                    }}
    .value;
}

void PioStatemachine::clock_divider_register(uint32_t data)
{
  Register<SMClockDivider> reg{.value = data};
  clock_divider_register_.frac = reg.reg.frac;
  clock_divider_register_.integral = reg.reg.integral;

  double integral = static_cast<double>(clock_divider_register_.integral);
  if (clock_divider_register_.integral == 0)
  {
    integral = 65536;
  }

  double frac = clock_divider_register_.frac;
  clock_divider_ = integral + (frac / 256);

  renode_log(LogLevel::Debug,
             std::format("SM{}: changed clock divider to: {}", id_, clock_divider_));
}

uint32_t PioStatemachine::exec_control_register() const
{
  return Register<SMExecControl>{
    .reg =
      {
        .status_n = exec_control_register_.status_n,
        .status_sel = exec_control_register_.status_sel,
        ._reserved = 0,
        .wrap_bottom = exec_control_register_.wrap_bottom,
        .wrap_top = exec_control_register_.wrap_top,
        .out_sticky = exec_control_register_.out_sticky,
        .inline_out_en = exec_control_register_.inline_out_en,
        .out_en_sel = exec_control_register_.out_en_sel,
        .jump_pin = exec_control_register_.jump_pin,
        .side_pindir = exec_control_register_.side_pindir,
        .side_en = exec_control_register_.side_en,
        .exec_stalled = exec_control_register_.exec_stalled,
      }}
    .value;
}

void PioStatemachine::exec_control_register(uint32_t data)
{
  Register<SMExecControl> reg = {.value = data};

  exec_control_register_.status_n = reg.reg.status_n;
  exec_control_register_.status_sel = reg.reg.status_sel;
  exec_control_register_.wrap_bottom = reg.reg.wrap_bottom;
  exec_control_register_.wrap_top = reg.reg.wrap_top;
  exec_control_register_.out_sticky = reg.reg.out_sticky;
  exec_control_register_.inline_out_en = reg.reg.inline_out_en;
  exec_control_register_.out_en_sel = reg.reg.out_en_sel;
  exec_control_register_.jump_pin = reg.reg.jump_pin;
  exec_control_register_.side_pindir = reg.reg.side_pindir;
  exec_control_register_.side_en = reg.reg.side_en;
  exec_control_register_.exec_stalled = reg.reg.exec_stalled;
  log(LogLevel::Warning, std::format("EXEC registers: {:x}, sideen: {}", data,
                                     exec_control_register_.side_en));
}

uint32_t PioStatemachine::shift_control_register() const
{
  return Register<SMShiftControl>{
    .reg =
      {
        ._reserved = 0,
        .autopush = shift_control_register_.autopush,
        .autopull = shift_control_register_.autopull,
        .in_shiftdir = shift_control_register_.in_shiftdir,
        .out_shiftdir = shift_control_register_.out_shiftdir,
        .push_threshold = shift_control_register_.push_threshold,
        .pull_threshold = shift_control_register_.pull_threshold,
        .fjoin_tx = shift_control_register_.fjoin_tx,
        .fjoin_rx = shift_control_register_.fjoin_rx,
      }}
    .value;
}

void PioStatemachine::shift_control_register(uint32_t data)
{
  Register<SMShiftControl> reg{.value = data};

  shift_control_register_.autopush = reg.reg.autopush;
  shift_control_register_.autopull = reg.reg.autopull;
  shift_control_register_.in_shiftdir = reg.reg.in_shiftdir;
  shift_control_register_.out_shiftdir = reg.reg.out_shiftdir;
  shift_control_register_.push_threshold =
    reg.reg.push_threshold == 0 ? 32 : reg.reg.push_threshold;
  shift_control_register_.pull_threshold =
    reg.reg.pull_threshold == 0 ? 32 : reg.reg.pull_threshold;
  shift_control_register_.fjoin_tx = reg.reg.fjoin_tx;
  shift_control_register_.fjoin_rx = reg.reg.fjoin_rx;

  if (shift_control_register_.fjoin_tx)
  {
    tx_.resize(8);
    rx_.resize(0);
  }

  if (shift_control_register_.fjoin_rx)
  {
    tx_.resize(0);
    rx_.resize(8);
  }
}

uint32_t PioStatemachine::pin_control_register() const
{
  return Register<SMPinControl>{
    .reg = {.out_base = pin_control_register_.out_base,
            .set_base = pin_control_register_.set_base,
            .sideset_base = pin_control_register_.sideset_base,
            .in_base = pin_control_register_.in_base,
            .out_count = pin_control_register_.out_count,
            .set_count = pin_control_register_.set_count,
            .sideset_count = pin_control_register_.sideset_count}}
    // namespace piosim
    .value;
}

void PioStatemachine::pin_control_register(uint32_t data)
{
  Register<SMPinControl> reg{.value = data};
  pin_control_register_.out_base = reg.reg.out_base;
  pin_control_register_.set_base = reg.reg.set_base;
  pin_control_register_.sideset_base = reg.reg.sideset_base;
  pin_control_register_.in_base = reg.reg.in_base;
  pin_control_register_.out_count = reg.reg.out_count;
  pin_control_register_.set_count = reg.reg.set_count;
  pin_control_register_.sideset_count = reg.reg.sideset_count;
}

uint8_t PioStatemachine::program_counter() const
{
  return program_counter_;
}

uint16_t PioStatemachine::current_instruction() const
{
  return current_instruction_;
}

void PioStatemachine::execute_immediately(uint16_t instruction)
{
  current_instruction_ = instruction;
  run_step();
}

} // namespace piosim
