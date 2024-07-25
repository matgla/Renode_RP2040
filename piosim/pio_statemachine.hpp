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

struct IOSync
{
  std::mutex mutex;
  std::condition_variable cv;
  bool sync;
  std::function<void(const std::function<void()> &)> schedule_action;
};

class PioStatemachine
{
public:
  PioStatemachine(int id, std::span<const uint16_t> program, std::span<bool> irqs);
  ~PioStatemachine();

  void enable(bool enable);
  void restart();
  void clock_divider_restart();

  bool step();

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
  inline bool run_step();

  inline bool process_delay();
  inline void schedule_delay(uint16_t delay);

  inline void increment_program_counter();

  inline void process_sideset(uint16_t data);
  inline bool jump_condition(uint8_t condition);
  inline bool process_jump(uint16_t data);
  inline bool process_wait(uint16_t data);
  inline bool process_in(uint16_t data);
  inline bool process_out(uint16_t data);
  inline bool process_pushpull(uint16_t data);
  inline bool process_push(uint16_t data);
  inline bool process_pull(uint16_t data);
  inline bool process_mov(uint16_t data);
  inline bool process_irq(uint16_t data);
  inline bool process_set(uint16_t data);

  inline void push_isr();
  inline void load_osr(uint32_t value);
  inline void load_osr();
  inline void write_isr(uint32_t bits, uint32_t data);
  inline uint32_t read_osr(uint32_t bits);

  inline uint32_t get_from_source(uint32_t source);

  int id_;

  bool enabled_;
  double clock_divider_;
  bool stalled_;
  bool sideset_done_;
  bool ignore_delay_;

  uint8_t program_counter_;
  std::optional<uint16_t> immediate_instruction_;
  std::optional<uint8_t> wait_for_irq_;
  std::span<const uint16_t> program_;
  std::span<bool> irqs_;

  uint32_t x_;
  uint32_t y_;
  uint32_t osr_;
  uint32_t isr_;
  uint32_t osr_counter_;
  uint32_t isr_counter_;

  uint32_t delay_counter_;
  uint32_t delay_;

  Fifo tx_;
  Fifo rx_;

  Register<SMClockDivider> clock_divider_register_;
  Register<SMExecControl> exec_control_register_;
  Register<SMShiftControl> shift_control_register_;
  Register<SMPinControl> pin_control_register_;

  uint16_t current_instruction_;
};

} // namespace piosim
