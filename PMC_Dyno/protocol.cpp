#include "protocol.h"
#include <string.h>
#include "bytes.h"

namespace {

void writeHeader(uint8_t type, size_t bodySize, uint8_t* out) {
    out[0] = SYNC_0;
    out[1] = SYNC_1;
    out[2] = type;
    out[3] = (uint8_t)bodySize;
}

// [0..2] position, [3..5] force (24-bit signed), [6..9] time (32-bit unsigned)
void writePoint(const Point& p, uint8_t* out) {
    writeInt24BE(p.position, out);
    writeInt24BE(p.force, out + 3);
    writeUint32BE(p.timeUs, out + 6);
}

} // namespace

size_t encodeDataFrame(const Point* points, size_t count, uint8_t* out, size_t outSize) {
    const size_t bodySize = count * POINT_WIRE_SIZE;
    if (count == 0 || count > MAX_POINTS_PER_FRAME || HEADER_SIZE + bodySize > outSize) {
        return 0;
    }
    writeHeader(TYPE_DATA, bodySize, out);
    for (size_t i = 0; i < count; i++) {
        writePoint(points[i], out + HEADER_SIZE + i * POINT_WIRE_SIZE);
    }
    return HEADER_SIZE + bodySize;
}

size_t encodeCommandFrame(uint8_t flag, const uint8_t* payload, size_t payloadSize,
                          uint8_t* out, size_t outSize) {
    const size_t bodySize = 1 + payloadSize;
    if (payloadSize > MAX_COMMAND_PAYLOAD || HEADER_SIZE + bodySize > outSize) {
        return 0;
    }
    writeHeader(TYPE_COMMAND, bodySize, out);
    out[HEADER_SIZE] = flag;
    if (payloadSize > 0) {
        memcpy(out + HEADER_SIZE + 1, payload, payloadSize);
    }
    return HEADER_SIZE + bodySize;
}

void FrameDecoder::reset() {
    step_ = Step::Sync0;
    type_ = 0;
    size_ = 0;
    received_ = 0;
}

bool FrameDecoder::feed(uint8_t byte, Command& out) {
    switch (step_) {
        case Step::Sync0:
            if (byte == SYNC_0) {
                step_ = Step::Sync1;
            }
            return false;

        case Step::Sync1:
            if (byte == SYNC_1) {
                step_ = Step::Type;
            } else if (byte != SYNC_0) { // 0xFF 0xFF 0x00 still resyncs
                step_ = Step::Sync0;
            }
            return false;

        case Step::Type:
            if (byte == TYPE_DATA || byte == TYPE_COMMAND) {
                type_ = byte;
                step_ = Step::Size;
            } else {
                step_ = Step::Sync0;
            }
            return false;

        case Step::Size:
            size_ = byte;
            received_ = 0;
            if (size_ == 0) {
                // A command always holds its flag; an empty data frame has nothing in it.
                reset();
                return false;
            }
            step_ = Step::Body;
            return false;

        case Step::Body:
            body_[received_++] = byte;
            if (received_ < size_) {
                return false;
            }
            {
                const bool isCommand = (type_ == TYPE_COMMAND);
                if (isCommand) {
                    out.flag = body_[0];
                    out.payloadSize = size_ - 1;
                    memcpy(out.payload, body_ + 1, out.payloadSize);
                }
                reset();
                return isCommand;
            }
    }
    return false;
}
