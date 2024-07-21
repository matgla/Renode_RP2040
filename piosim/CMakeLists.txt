#
# CMakeLists.txt
#
# Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
#
# Distributed under the terms of the MIT License.
#

cmake_minimum_required(VERSION 3.28)

project (piosim C CXX)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED)
set(CMAKE_CXX_FLAGS "-Wall -Wextra -Werror")
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

add_library(piosim SHARED)


target_sources(piosim
  PUBLIC
    decoder.hpp
    pio_renode_glue.hpp
    pio_registers.hpp
    pio_statemachine.hpp
    pio.hpp
    fifo.hpp
  PRIVATE 
    decoder.cpp
    pio_renode_glue.cpp
    pio_statemachine.cpp
    pio.cpp
    fifo.cpp
)
