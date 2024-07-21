/**
 * pio.cpp
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

#include "pio.hpp"

#include "renode_log.hpp"

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

} // namespace piosim
