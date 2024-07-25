/**
 * fifo.hpp
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

#pragma once

#include <cstdint>
#include <queue>

namespace piosim
{

class Fifo
{
public:
  Fifo();

  std::size_t max_size() const;
  void push(uint32_t data);
  uint32_t pop();

  bool empty() const;
  bool full() const;
  std::size_t size() const;
  void resize(std::size_t size);

private:
  std::size_t max_size_;
  std::queue<uint32_t> queue_;
};

} // namespace piosim
