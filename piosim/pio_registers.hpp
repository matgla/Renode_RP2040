/**
 * pio_registers.hpp
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

#pragma once

#include <cstdint>
#include <functional>

namespace piosim
{

struct RegisterHolder
{
  std::function<uint32_t()> read;
  std::function<void(uint32_t)> write;
};

struct SMClockDivider
{
  uint32_t _r : 8;
  uint32_t frac : 8;
  uint32_t integral : 16;
};

struct SMClockDividerInternal
{
  uint8_t frac;
  uint16_t integral;
};

struct SMExecControlInternal
{
  uint8_t status_n;
  bool status_sel;
  uint8_t wrap_bottom;
  uint8_t wrap_top;
  bool out_sticky;
  bool inline_out_en;
  uint8_t out_en_sel;
  uint8_t jump_pin;
  bool side_pindir;
  bool side_en;
  bool exec_stalled;
};

struct SMExecControl
{
  uint32_t status_n : 4;
  uint32_t status_sel : 1;
  uint32_t _reserved : 2;
  uint32_t wrap_bottom : 5;
  uint32_t wrap_top : 5;
  uint32_t out_sticky : 1;
  uint32_t inline_out_en : 1;
  uint32_t out_en_sel : 5;
  uint32_t jump_pin : 5;
  uint32_t side_pindir : 1;
  uint32_t side_en : 1;
  uint32_t exec_stalled : 1;
};

struct SMShiftControlInternal
{
  bool autopush;
  bool autopull;
  bool in_shiftdir;
  bool out_shiftdir;
  uint8_t push_threshold;
  uint8_t pull_threshold;
  bool fjoin_tx;
  bool fjoin_rx;
};

struct SMShiftControl
{
  uint32_t _reserved : 16;
  uint32_t autopush : 1;
  uint32_t autopull : 1;
  uint32_t in_shiftdir : 1;
  uint32_t out_shiftdir : 1;
  uint32_t push_threshold : 5;
  uint32_t pull_threshold : 5;
  uint32_t fjoin_tx : 1;
  uint32_t fjoin_rx : 1;
};

struct SMPinControlInternal
{
  uint8_t out_base;
  uint8_t set_base;
  uint8_t sideset_base;
  uint8_t in_base;
  uint8_t out_count;
  uint8_t set_count;
  uint8_t sideset_count;
};

struct SMPinControl
{
  uint32_t out_base : 5;
  uint32_t set_base : 5;
  uint32_t sideset_base : 5;
  uint32_t in_base : 5;
  uint32_t out_count : 6;
  uint32_t set_count : 3;
  uint32_t sideset_count : 3;
};

struct Control
{
  uint32_t sm_enable : 4;
  uint32_t sm_restart : 4;
  uint32_t clkdiv_restart : 4;
  uint32_t _reserved : 20;
};

struct Fstat
{
  uint32_t rx_full : 4;
  uint32_t _r1 : 4;
  uint32_t rx_empty : 4;
  uint32_t _r2 : 4;
  uint32_t tx_full : 4;
  uint32_t _r3 : 4;
  uint32_t tx_empty : 4;
  uint32_t _r4 : 4;
};

template <typename T>
struct Register
{
  static_assert(sizeof(T) == sizeof(uint32_t), "Register must be 32 bit size");
  union {
    T reg;
    uint32_t value;
  };
};

} // namespace piosim
