/**
 * pio.cpp
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */
#include "pio.hpp"
#include "renode_log.hpp"

#include <algorithm>
#include <bitset>
#include <format>
#include <functional>
#include <span>
#include <sstream>

namespace piosim
{

PioSimulator &PioSimulator::get()
{
  if (!self_)
  {
    init();
  }
  return *self_;
}

void PioSimulator::init()
{
  self_ = std::unique_ptr<PioSimulator>(new PioSimulator());
}

void PioSimulator::close()
{
  if (!self_)
  {
    return;
  }

  for (auto &sm : self_->sm_)
  {
    sm.enable(false);
  }

  self_.reset();
}

PioSimulator::PioSimulator()
  : program_{}
  , irqs_{}
  , actions_{}
  , control_{.reg =
               {
                 .sm_enable = 0,
                 .sm_restart = 0,
                 .clkdiv_restart = 0,
                 ._reserved = 0,
               }}
  , sm_{
      PioStatemachine{0, program_, irqs_},
      PioStatemachine{1, program_, irqs_},
      PioStatemachine{2, program_, irqs_},
      PioStatemachine{3, program_, irqs_},
    }
{
  actions_[Address::CTRL] = {
    .read = std::bind(&PioSimulator::read_control, this),
    .write = std::bind(&PioSimulator::write_control, this, std::placeholders::_1),
  };

  actions_[Address::FSTAT] = {
    .read = std::bind(&PioSimulator::read_fstat, this),
    .write = nullptr,
  };

  actions_[Address::FLEVEL] = {
    .read = std::bind(&PioSimulator::read_flevel, this),
    .write = nullptr,
  };

  for (uint32_t i = 0; i < sm_.size(); ++i)
  {
    actions_[Address::TXF0 + i * 4] = {
      .read = nullptr,
      .write =
        [this, i](uint32_t data) {
          sm_[i].push_tx(data);
        },
    };

    actions_[Address::RXF0 + i * 4] = {
      .read =
        [this, i]() {
          uint32_t r = sm_[i].pop_rx();
          renode_log(LogLevel::Error, std::format("!!! RX read: {:x}", r));
          return r;
        },
      .write = nullptr,
    };
  }

  for (uint32_t i = 0; i < 32; ++i)
  {
    actions_[Address::INSTR_MEM0 + i * 4] = {
      .read = nullptr,
      .write =
        [this, i](uint32_t data) {
          program_[i] = data;
        },
    };
  }

  for (uint32_t i = 0; i < sm_.size(); ++i)
  {
    actions_[Address::SM0_CLKDIV + i * 0x18] = {
      .read =
        [&sm = sm_[i]] {
          return sm.clock_divider_register();
        },
      .write =
        [&sm = sm_[i]](uint32_t data) {
          sm.clock_divider_register(data);
        },
    };

    actions_[Address::SM0_EXECCTRL + i * 0x18] = {
      .read =
        [&sm = sm_[i]] {
          return sm.exec_control_register();
        },
      .write =
        [&sm = sm_[i]](uint32_t value) {
          sm.exec_control_register(value);
        },
    };

    actions_[Address::SM0_SHIFTCTRL + i * 0x18] = {
      .read =
        [&sm = sm_[i]] {
          return sm.shift_control_register();
        },
      .write =
        [&sm = sm_[i]](uint32_t value) {
          sm.shift_control_register(value);
        },
    };

    actions_[Address::SM0_SHIFTCTRL + i * 0x18] = {
      .read =
        [&sm = sm_[i]] {
          return sm.shift_control_register();
        },
      .write =
        [&sm = sm_[i]](uint32_t value) {
          sm.shift_control_register(value);
        },
    };

    actions_[Address::SM0_ADDR + i * 0x18] = {
      .read =
        [&sm = sm_[i]] {
          return sm.program_counter();
        },
      .write = nullptr,
    };

    actions_[Address::SM0_INSTR + i * 0x18] = {
      .read =
        [&sm = sm_[i]] {
          return sm.current_instruction();
        },
      .write =
        [&sm = sm_[i]](uint32_t value) {
          sm.execute_immediately(static_cast<uint16_t>(value));
        },
    };

    actions_[Address::SM0_PINCTRL + i * 0x18] = {
      .read =
        [&sm = sm_[i]] {
          return sm.pin_control_register();
        },
      .write =
        [&sm = sm_[i]](uint32_t value) {
          sm.pin_control_register(value);
        },
    };
  }

  renode_log(LogLevel::Debug, "Created PIOSIM emulator");
}

void PioSimulator::write_memory(uint32_t address, uint32_t value)
{
  if (actions_.contains(address) && actions_.at(address).write)
  {
    actions_.at(address).write(value);
    return;
  }

  renode_log(
    LogLevel::Warning,
    std::format("unhandled write at: 0x{:x}, value: 0x{:x}", address, value));
}

uint32_t PioSimulator::read_memory(uint32_t address) const
{
  if (actions_.contains(address) && actions_.at(address).read)
  {
    return actions_.at(address).read();
  }

  renode_log(LogLevel::Warning, std::format("unhandled read from: 0x{:x}", address));
  return 0;
}

uint32_t PioSimulator::execute(uint32_t steps)
{
  bool break_in_next = false;
  for (uint32_t i = 0; i < steps; ++i)
  {
    for (auto &sm : sm_)
    {
      if (!sm.step())
      {
        break_in_next = true;
      }
    }

    if (break_in_next)
    {
      return i + 1;
    }
  }

  return steps;
}

uint32_t PioSimulator::read_control() const
{
  return control_.value;
}

void PioSimulator::write_control(uint32_t value)
{
  control_.value = value;
  const std::bitset<4> enable{control_.reg.sm_enable};
  const std::bitset<4> restart{control_.reg.sm_restart};
  const std::bitset<4> clkdiv_restart{control_.reg.clkdiv_restart};
  for (uint32_t i = 0; i < sm_.size(); ++i)
  {
    sm_[i].enable(enable.test(i));
    if (restart.test(i))
      sm_[i].restart();
    if (clkdiv_restart.test(i))
      sm_[i].clock_divider_restart();
  }
}

uint32_t PioSimulator::read_fstat() const
{
  Register<Fstat> fstat{};

  for (uint32_t i = 0; i < sm_.size(); ++i)
  {
    fstat.reg.rx_full |= (sm_[i].rx_fifo().full() << i);
    fstat.reg.rx_empty |= (sm_[i].rx_fifo().empty() << i);
    fstat.reg.tx_full |= (sm_[i].tx_fifo().full() << i);
    fstat.reg.tx_empty |= (sm_[i].tx_fifo().empty() << i);
  }
  return fstat.value;
}

uint32_t PioSimulator::read_flevel() const
{
  uint32_t r = 0;
  for (uint32_t i = 0; i < sm_.size(); ++i)
  {
    r |= (sm_[i].tx_fifo().size() << (i * 8));
    r |= (sm_[i].rx_fifo().size() << (i * 8 + 4));
  }

  return r;
}

std::unique_ptr<PioSimulator> PioSimulator::self_ = nullptr;

} // namespace piosim
