/**
 * decoder.hpp
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

#pragma once

#include <string_view>

namespace piosim
{
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

inline std::string_view to_string(OpCode o)
{
  switch (o)
  {
  case OpCode::Jmp:
    return "JMP";
  case piosim::OpCode::Wait:
    return "WAIT";
  case OpCode::In:
    return "IN";
  case piosim::OpCode::Out:
    return "OUT";
  case piosim::PushPull:
    return "PUSHPULL";
  case piosim::Mov:
    return "MOV";
  case piosim::Irq:
    return "IRQ";
  case piosim::Set:
    return "SET";
  }
  return "UNK";
}

} // namespace piosim
