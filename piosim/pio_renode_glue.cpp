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
  void (*gpio_set_pindir_bitset)(uint32_t, uint32_t);
  void (*gpio_set_pin_bitset)(uint32_t, uint32_t);

  void renode_external_attach__ActionInt32String__LogAsCpu(
    void (*callback)(int, const char *))
  {
    log_as_cpu = callback;
  }

  void renode_external_attach__ActionUInt32UInt32__GpioPinWriteBitset(
    void (*callback)(uint32_t, uint32_t))
  {
    gpio_set_pin_bitset = callback;
  }

  void renode_external_attach__ActionUInt32UInt32__GpioPindirWriteBitset(
    void (*callback)(uint32_t, uint32_t))
  {
    gpio_set_pindir_bitset = callback;
  }

  void pio_initialize_ex()
  {
    piosim::PioSimulator::get();
  }

  void pio_deinitialize_ex()
  {
    piosim::PioSimulator::get().close();
  }

  uint32_t pio_execute_ex(uint32_t number_of_instructions)
  {
    return piosim::PioSimulator::get().execute(number_of_instructions);
  }

  uint32_t pio_read_memory_ex(uint32_t address)
  {
    return piosim::PioSimulator::get().read_memory(address);
  }

  void pio_write_memory_ex(uint32_t address, uint32_t value)
  {
    piosim::PioSimulator::get().write_memory(address, value);
  }
}

void renode_log(LogLevel level, std::string_view message)
{
  log_as_cpu(static_cast<int>(level), message.data());
}

void renode_gpio_set_pindir_bitset(uint32_t bitset, uint32_t bitmap)
{
  gpio_set_pindir_bitset(bitset, bitmap);
}

void renode_gpio_set_pin_bitset(uint32_t bitset, uint32_t bitmap)
{
  gpio_set_pin_bitset(bitset, bitmap);
}
