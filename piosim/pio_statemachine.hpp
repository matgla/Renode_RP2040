/**
 * pio_statemachine.hpp
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

#pragma once

#include <cstdint>

#include <atomic>
#include <condition_variable>
#include <mutex>
#include <optional>
#include <span>
#include <thread>

#include "fifo.hpp"
#include "pio_registers.hpp"

namespace piosim
{

class PioStatemachine
{
public:
  PioStatemachine(int id, std::span<const uint16_t> program);

  void enable(bool enable);
  void restart();
  void clock_divider_restart();

  void step();
  void execute(uint32_t steps);
  void wait_for_done();

  const Fifo &tx_fifo() const;
  const Fifo &rx_fifo() const;

  void push_tx(uint32_t data);
  uint32_t pop_rx();

  uint32_t clock_divider_register() const;
  void clock_divider_register(uint32_t data);

  uint32_t exec_control_register() const;
  void exec_control_register(uint32_t data);

  uint32_t shift_control_register() const;
  void shift_control_register(uint32_t data);

  uint32_t pin_control_register() const;
  void pin_control_register(uint32_t data);

  uint8_t program_counter() const;
  uint16_t current_instruction() const;
  void execute_immediately(uint16_t instruction);

private:
  void loop();

  void pause();
  void resume();

  bool process_delay();
  void schedule_delay(uint16_t delay);

  void increment_program_counter();

  void process_sideset(uint16_t data);

  int id_;

  bool running_;
  bool stop_;
  bool enabled_;
  std::atomic<double> clock_divider_;
  std::atomic<bool> stalled_;
  bool sideset_done_;
  bool ignore_delay_;
  bool request_pause_;

  uint8_t program_counter_;
  std::optional<uint16_t> immediate_instruction_;
  std::span<const uint16_t> program_;

  std::mutex mutex_;
  std::condition_variable cv_;
  std::thread thread_;
  uint32_t scheduleSteps_;

  uint32_t x_;
  uint32_t y_;
  uint32_t osr_;
  uint32_t isr_;

  uint32_t delay_counter_;
  uint32_t delay_;

  Fifo tx_;
  Fifo rx_;

  Register<SMClockDivider> clock_divider_register_;
  Register<SMExecControl> exec_control_register_;
  Register<SMShiftControl> shift_control_register_;
  Register<SMPinControl> pin_control_register_;
};

} // namespace piosim
