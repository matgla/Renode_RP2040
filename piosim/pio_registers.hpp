/**
 * pio_registers.hpp
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

#pragma once

#include <map>
#include <memory>

#include <string_view>

#include "pio_iregister.hpp"

namespace piosim
{

struct RegisterHolder
{
  const std::string_view name;
  std::unique_ptr<IRegister> reg;
};

class PioRegisters
{
public:
  PioRegisters();
  void add(RegisterHolder &&holder);

private:
  std::map<int, RegisterHolder> registers_;
};

} // namespace piosim
