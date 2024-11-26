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

#include <iostream>

extern "C"
{
  void (*log_as_cpu)(int, const char *);
  void (*gpio_set_pindir_bitset)(uint32_t, uint32_t);
  void (*gpio_set_pin_bitset)(uint32_t, uint32_t);
  int (*gpio_get_pin_state)(uint32_t);
  uint32_t (*gpio_get_pin_bitmap)();

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

  void renode_external_attach__FuncInt32UInt32__GpioGetPinState(
    int (*callback)(uint32_t))
  {
    gpio_get_pin_state = callback;
  }

  void renode_external_attach__FuncUInt32__GetGpioPinBitmap(uint32_t (*callback)())
  {
    gpio_get_pin_bitmap = callback;
  }

  void pio_initialize_ex(int id)
  {
    piosim::PioSimulator::init(id);
  }

  void pio_deinitialize_ex(int id)
  {
    piosim::PioSimulator::close(id);
  }

  void pio_reset_ex(int id)
  {
    piosim::PioSimulator::reset(id);
  }

  uint32_t pio_execute_ex(int id, uint32_t number_of_instructions)
  {
    return piosim::PioSimulator::get(id).execute(number_of_instructions);
  }

  uint32_t pio_read_memory_ex(int id, uint32_t address)
  {
    return piosim::PioSimulator::get(id).read_memory(address);
  }

  void pio_write_memory_ex(int id, uint32_t address, uint32_t value)
  {
    piosim::PioSimulator::get(id).write_memory(address, value);
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

bool renode_gpio_get_pin_state(uint32_t pin)
{
  return gpio_get_pin_state(pin) == 1;
}

uint32_t renode_gpio_get_pin_bitmap()
{
  return gpio_get_pin_bitmap();
}

