/**
 * pio_iregister.hpp
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

#pragma once

#include <cstdint>

namespace piosim
{

class IRegister
{
public:
  virtual ~IRegister() = default;

  virtual void write(uint32_t address, uint32_t value) = 0;
  virtual uint32_t read(uint32_t address);
};

} // namespace piosim
