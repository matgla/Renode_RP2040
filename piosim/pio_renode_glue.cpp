/**
 * pio.cpp
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

#include "pio_renode_glue.hpp"

#include "renode_log.hpp"

#include "pio.hpp"

extern "C"
{
  void (*log_as_cpu)(int, const char *);
  void renode_external_attach__ActionInt32String__LogAsCpu(
    void (*callback)(int, const char *))
  {
    log_as_cpu = callback;
  }

  void pio_initialize_ex()
  {
    piosim::PioSimulator::get();
  }

  uint32_t pio_execute_ex(uint32_t number_of_instructions)
  {
    return number_of_instructions;
  }

  uint32_t pio_read_memory_ex(uint32_t address)
  {
    return 0;
  }

  void pio_write_memory_ex(uint32_t address, uint32_t value)
  {
  }
}

void renode_log(LogLevel level, std::string_view message)
{
  log_as_cpu(static_cast<int>(level), message.data());
}
