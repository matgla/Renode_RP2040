/**
 * pio.cpp
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

#include "pio.hpp"

#include "renode_log.hpp"

#include <format>

namespace piosim
{

PioSimulator &PioSimulator::get()
{
  static PioSimulator sim;
  return sim;
}

PioSimulator::PioSimulator()
{
  renode_log(LogLevel::Debug, "Created PIOSIM emulator");
}

void PioSimulator::write_memory(uint32_t address, uint32_t value)
{
  renode_log(LogLevel::Warning,
             std::format("unhandled write at: {}, value: {}", address, value));
}

uint32_t PioSimulator::read_memory(uint32_t address)
{
  std::stringstream s;
  s << "unhandled read from: " << std::hex << address;
  renode_log(LogLevel::Warning, s.str());
  return 0;
}

} // namespace piosim
