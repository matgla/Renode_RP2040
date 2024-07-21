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

} // namespace

PioStatemachine::PioStatemachine(int id, std::span<const uint16_t> program)
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
  , program_{program}
  , mutex_{}
  , cv_{}
  , thread_{}
  , scheduleSteps_{0}
  , x_{0}
  , y_{0}
  , osr_{0}
  , isr_{0}
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
{
}

void PioStatemachine::enable(bool enable)
{
  if (enable && !enabled_)
  {
    renode_log(LogLevel::Debug, std::format("enabling SM{}", id_));
    enabled_ = true;
    thread_ = std::thread(&PioStatemachine::loop, this);
  }
  else if (enabled_)
  {
    renode_log(LogLevel::Debug, std::format("disabling SM{}", id_));
    stop_ = true;
    enabled_ = false;
    cv_.notify_one();
    thread_.join();
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

        if (exec_control_register_.reg.side_pindir && enabled)
        {
          renode_gpio_set_pindir_bitset(gpio_bitset, gpio_bitmap);
        }
        else
        {
          renode_gpio_set_pin_bitset(gpio_bitmap, gpio_bitmap);
        }
      }
    }
    sideset_done_ = true;
  }
}

void PioStatemachine::step()
{
  if (!process_delay())
  {
    return;
  }

  const uint16_t instruction =
    immediate_instruction_ ? *immediate_instruction_ : program_[program_counter_];
  immediate_instruction_.reset();

  const PioDecodedInstruction cmd{instruction};
  process_sideset(cmd.delay_or_sideset);

  bool finished = false;

  switch (cmd.opcode)
  {
  case PioDecodedInstruction::OpCode::Jmp: {
    break;
  }
  case PioDecodedInstruction::OpCode::Wait: {
    break;
  }
  case PioDecodedInstruction::OpCode::In: {
    break;
  }

  case PioDecodedInstruction::OpCode::Out: {
    break;
  }
  case PioDecodedInstruction::OpCode::PushPull: {
    break;
  }

  case PioDecodedInstruction::OpCode::Mov: {
    break;
  }
  case PioDecodedInstruction::OpCode::Irq: {
    break;
  }
  case PioDecodedInstruction::OpCode::Set: {
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
  }
  else
  {
    stalled_ = true;
  }
}

void PioStatemachine::execute(uint32_t steps)
{
  wait_for_done();
  scheduleSteps_ = steps;
  cv_.notify_one();
}

void PioStatemachine::wait_for_done()
{
  std::unique_lock<std::mutex> lk(mutex_);
  cv_.wait(lk, [this] {
    return !running_;
  });
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
  pause();
  tx_.push(data);
  resume();
}

uint32_t PioStatemachine::pop_rx()
{
  pause();
  uint32_t r = rx_.pop();
  resume();
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
  pause();
  exec_control_register_.value = (data & 0x7fffffff);
  resume();
}

uint32_t PioStatemachine::shift_control_register() const
{
  return shift_control_register_.value;
}

void PioStatemachine::shift_control_register(uint32_t data)
{
  pause();
  shift_control_register_.value = data;
  resume();
}

uint32_t PioStatemachine::pin_control_register() const
{
  return pin_control_register_.value;
}

void PioStatemachine::pin_control_register(uint32_t data)
{
  pause();
  pin_control_register_.value = data;
  resume();
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
  pause();
  immediate_instruction_ = instruction;
  resume();
}

void PioStatemachine::loop()
{
  std::unique_lock<std::mutex> lk(mutex_);
  while (!stop_)
  {
    cv_.wait(lk, [this] {
      return scheduleSteps_ > 0 || stop_;
    });

    if (stop_)
    {
      return;
    }

    running_ = true;
    for (uint32_t i = 0; i < scheduleSteps_; ++i)
    {
      if (request_pause_)
      {
        running_ = false;
        lk.unlock();
        cv_.notify_one();
        lk.lock();
        cv_.wait(lk, [this] {
          return !request_pause_;
        });
        running_ = true;
      }
      step();
    }

    scheduleSteps_ = 0;
    running_ = false;

    lk.unlock();
    cv_.notify_one();
    lk.lock();
  }
}

void PioStatemachine::pause()
{
  std::unique_lock<std::mutex> lk(mutex_);
  request_pause_ = true;
  cv_.wait(lk, [this] {
    return !running_;
  });
}

void PioStatemachine::resume()
{
  request_pause_ = false;
  cv_.notify_one();
}

} // namespace piosim
