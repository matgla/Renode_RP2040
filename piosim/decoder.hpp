/**
 * decoder.hpp
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

#pragma once

#include <cstdint>

namespace piosim
{

class PioDecodedInstruction
{
public:
  enum OpCode
  {
    Jmp = 0x0,
    Wait = 0x1,
    In = 0x2,
    Out = 0x3,
    PushPull = 0x4,
    Mov = 0x5,
    Irq = 0x6,
    Set = 0x7,
  };

  PioDecodedInstruction(uint16_t data);

  const OpCode opcode;
  const uint16_t delay_or_sideset;
  const uint16_t immediate_data;
};

} // namespace piosim
