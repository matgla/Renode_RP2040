/**
 * renode_log.hpp
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

#pragma once

#include <string_view>

enum class LogLevel : int
{
  Noisy = -1,
  Debug = 0,
  Info = 1,
  Warning = 2,
  Error = 3
};

void renode_log(LogLevel level, std::string_view message);
