#include "data_buffer.h"
#include <freertos/FreeRTOS.h>
#include <freertos/semphr.h>

namespace {

struct Lock {
    explicit Lock(void* m) : mutex((SemaphoreHandle_t)m) { xSemaphoreTake(mutex, portMAX_DELAY); }
    ~Lock() { xSemaphoreGive(mutex); }
    SemaphoreHandle_t mutex;
};

} // namespace

void PointBuffer::begin() {
    mutex_ = xSemaphoreCreateMutex();
}

bool PointBuffer::push(const Point& p) {
    Lock lock(mutex_);
    if (count_ >= CAPACITY) {
        dropped_ = dropped_ + 1;
        return false;
    }
    points_[(head_ + count_) % CAPACITY] = p;
    count_++;
    return true;
}

size_t PointBuffer::take(Point* out, bool partial) {
    Lock lock(mutex_);
    size_t n = count_;
    if (n >= MAX_POINTS_PER_FRAME) {
        n = MAX_POINTS_PER_FRAME;
    } else if (!partial) {
        return 0;
    }
    for (size_t i = 0; i < n; i++) {
        out[i] = points_[(head_ + i) % CAPACITY];
    }
    head_ = (head_ + n) % CAPACITY;
    count_ -= n;
    return n;
}

void PointBuffer::reset() {
    Lock lock(mutex_);
    head_ = 0;
    count_ = 0;
    dropped_ = 0;
}
