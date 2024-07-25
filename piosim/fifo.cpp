/**
 * fifo.cpp
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

#include "fifo.hpp"

namespace piosim
{

Fifo::Fifo()
  : max_size_{4}
  , queue_{}
{
}

std::size_t Fifo::max_size() const
{
  return max_size_;
}

void Fifo::push(uint32_t data)
{
  if (queue_.size() < max_size_)
  {
    queue_.push(data);
  }
}

uint32_t Fifo::pop()
{
  if (queue_.empty())
  {
    return 0;
  }
  uint32_t r = queue_.front();
  queue_.pop();
  return r;
}

bool Fifo::empty() const
{
  return queue_.empty();
}

bool Fifo::full() const
{
  return queue_.size() == max_size_;
}

void Fifo::resize(std::size_t size)
{
  std::queue<uint32_t> q;
  queue_.swap(q);
  max_size_ = size;
}

std::size_t Fifo::size() const
{
  return queue_.size();
}

} // namespace piosim
