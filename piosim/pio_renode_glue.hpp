/**
 * pio.hpp
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

#pragma once

#include <cstdint>

extern "C"
{
  void pio_initialize_ex(int id);
  uint32_t pio_execute_ex(int id, uint32_t number_of_instructions);
  uint32_t pio_read_memory_ex(int id, uint32_t address);
  void pio_write_memory_ex(int id, uint32_t address, uint32_t value);

} // extern "C"
