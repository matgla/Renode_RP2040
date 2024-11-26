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

if(CMAKE_HOST_WIN32)
  set(CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS ON)
  add_link_options("-static")
endif()

if (MSVC)
  set(CMAKE_CXX_FLAGS "/W4 /WX")
  set(CMAKE_CXX_FLAGS_RELEASE "/O2")
  set(CMAKE_CXX_FLAGS_DEBUG "/Od")
else ()
  set(CMAKE_CXX_FLAGS "-Wall -Wextra -Werror")
  set(CMAKE_CXX_FLAGS_RELEASE "-O3")
  set(CMAKE_CXX_FLAGS_DEBUG "-Og -g")
endif ()

set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

add_library(piosim SHARED)

target_sources(piosim
  PUBLIC
    opcode.hpp
    pio_renode_glue.hpp
    pio_registers.hpp
    pio_statemachine.hpp
    pio.hpp
    fifo.hpp
  PRIVATE 
    pio_renode_glue.cpp
    pio_statemachine.cpp
    pio.cpp
    fifo.cpp
)

target_include_directories(piosim 
  PUBLIC 
    ${PROJECT_SOURCE_DIR}
)

if (ENABLE_TESTS)
  add_subdirectory(tests)
endif ()
