/**
 * pio.hpp
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

#pragma once

#include <array>
#include <cstdint>
#include <map>

#include "pio_registers.hpp"
#include "pio_statemachine.hpp"

namespace piosim
{

class PioSimulator
{
public:
  static PioSimulator &get();

  void write_memory(uint32_t address, uint32_t value);
  uint32_t read_memory(uint32_t address) const;
  uint32_t execute(uint32_t steps);
  void close();

private:
  PioSimulator();

  uint32_t read_control() const;
  void write_control(uint32_t value);
  uint32_t read_fstat() const;
  uint32_t read_flevel() const;

  enum Address : uint32_t
  {
    CTRL = 0x000,
    FSTAT = 0x004,
    FLEVEL = 0x00c,
    TXF0 = 0x010,
    RXF0 = 0x020,
    INSTR_MEM0 = 0x048,
    SM0_CLKDIV = 0x0c8,
    SM0_EXECCTRL = 0x0cc,
    SM0_SHIFTCTRL = 0x0d0,
    SM0_ADDR = 0x0d4,
    SM0_INSTR = 0x0d8,
    SM0_PINCTRL = 0x0dc
  };

  std::array<uint16_t, 32> program_;
  std::map<uint32_t, RegisterHolder> actions_;
  Register<Control> control_;

  std::array<PioStatemachine, 4> sm_;

  // program is read-only for statemachine, no need to synchronize thread
};

} // namespace piosim
