#
# CMakeLists.txt
#
# Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
#
# Distributed under the terms of the MIT License.
#

cmake_minimum_required(VERSION 3.28)

project (piosim C CXX)

add_library(piosim SHARED)

set(CMAKE_CXX_STANDARD "23")

set(CMAKE_CXX_FLAGS "-Wall -Wextra")

target_sources(piosim
  PUBLIC
    pio_renode_glue.hpp
    pio_registers.hpp
    pio.hpp
  PRIVATE 
    pio_renode_glue.cpp
    pio_registers.cpp
    pio.cpp
)
