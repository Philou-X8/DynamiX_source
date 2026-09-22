#include "data_buffer.h"
#include <freertos/FreeRTOS.h>
#include <freertos/semphr.h>
#include <string.h>

DoubleBuffer::DoubleBuffer()
    : writeIndex_(0), writeCount_(0) {
    mutex_ = xSemaphoreCreateMutex();
}

bool DoubleBuffer::push(const Point& p) {
    bool ok = false;
    xSemaphoreTake((SemaphoreHandle_t)mutex_, portMAX_DELAY);
    if (writeCount_ < MAX_POINTS_PER_BUFFER) {
        buffers_[writeIndex_][writeCount_] = p;
        writeCount_++;
        ok = true;
    }
    xSemaphoreGive((SemaphoreHandle_t)mutex_);
    return ok;
}

bool DoubleBuffer::swapLocked(Point* outBuffer, size_t& outCount, bool force) {
    bool didSwap = false;
    xSemaphoreTake((SemaphoreHandle_t)mutex_, portMAX_DELAY);
    if (writeCount_ > 0 && (force || writeCount_ == MAX_POINTS_PER_BUFFER)) {
        memcpy(outBuffer, buffers_[writeIndex_], writeCount_ * sizeof(Point));
        outCount = writeCount_;
        writeIndex_ = 1 - writeIndex_;
        writeCount_ = 0;
        didSwap = true;
    } else {
        outCount = 0;
    }
    xSemaphoreGive((SemaphoreHandle_t)mutex_);
    return didSwap;
}

bool DoubleBuffer::trySwap(Point* outBuffer, size_t& outCount) {
    return swapLocked(outBuffer, outCount, /*force=*/false);
}

bool DoubleBuffer::forceSwap(Point* outBuffer, size_t& outCount) {
    return swapLocked(outBuffer, outCount, /*force=*/true);
}
