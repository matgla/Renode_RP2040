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

target_sources(piosim
  PUBLIC
    pio_renode_glue.hpp  
    pio.hpp
  PRIVATE 
    pio_renode_glue.cpp
    pio.cpp
)
