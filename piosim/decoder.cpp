/**
 * decoder.cpp
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

#include "decoder.hpp"

namespace piosim
{

PioDecodedInstruction::PioDecodedInstruction(uint16_t data)
  : opcode{static_cast<OpCode>((data >> 13) & 0x7)}
  , delay_or_sideset{static_cast<uint16_t>((data >> 8) & 0x03)}
  , immediate_data{static_cast<uint16_t>(data & 0xff)}
{
}

} // namespace piosim
