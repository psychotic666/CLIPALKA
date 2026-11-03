#pragma once
#include <algorithm>
#include <deque>
#include <map>
#include <stdexcept>
#include <string>
#include <vector>

struct PcmBlock {
    std::vector<short> samples;
    // False only when the capture backend reports this source inactive/silent.
    bool active;
};

// Input PCM has already been converted to the SAME output rate/channel layout.
// Conversion can yield packets differing by one frame. Carry the remainder to
// the next call, rather than expanding a live microphone with artificial zeros.
class PcmBlockAligner {
    std::map<std::wstring, std::deque<short>> pending_;
public:
    void Clear() { pending_.clear(); }

    std::map<std::wstring, std::vector<short>> Align(
        const std::map<std::wstring, PcmBlock>& blocks,
        size_t channels, size_t maxBufferedFrames = 96000) {
        if (!channels || !maxBufferedFrames) throw std::invalid_argument("Invalid PCM alignment format");
        for (auto it=pending_.begin(); it!=pending_.end();) {
            if (blocks.find(it->first)==blocks.end()) it=pending_.erase(it);
            else ++it;
        }
        size_t common=static_cast<size_t>(-1), largest=0;
        bool hasActive=false;
        for (const auto& pair : blocks) {
            const auto& block=pair.second;
            if (block.samples.size()%channels) throw std::invalid_argument("Partial PCM frame");
            auto& queue=pending_[pair.first];
            if (queue.size()/channels + block.samples.size()/channels > maxBufferedFrames)
                throw std::runtime_error("Audio source stalled: PCM alignment buffer exceeded two seconds");
            queue.insert(queue.end(),block.samples.begin(),block.samples.end());
            largest=(std::max)(largest,queue.size());
            if(block.active) { common=(std::min)(common,queue.size()); hasActive=true; }
        }
        const size_t count=hasActive ? common : largest;
        std::map<std::wstring,std::vector<short>> result;
        for (const auto& pair : blocks) {
            auto& queue=pending_[pair.first];
            auto& output=result[pair.first];
            output.assign(count,0);
            const size_t take=(std::min)(queue.size(),count);
            std::copy_n(queue.begin(),take,output.begin());
            queue.erase(queue.begin(),queue.begin()+take);
        }
        return result;
    }
};
