/**
 * pio.hpp
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

#pragma once

#include <cstdint>

namespace piosim
{

class PioSimulator
{
public:
  static PioSimulator &get();

  void write_memory(uint32_t address, uint32_t value);
  uint32_t read_memory(uint32_t address);

private:
  PioSimulator();
};

} // namespace piosim
