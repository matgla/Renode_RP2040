/**
 * gpio.hpp
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

#pragma once

#include <cstdint>

void renode_gpio_set_pindir_bitset(uint32_t bitset, uint32_t bitmap);
void renode_gpio_set_pin_bitset(uint32_t bitset, uint32_t bitmap);
