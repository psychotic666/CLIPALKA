#include "../PcmBlockAligner.h"
#include <cassert>
#include <cmath>
#include <iostream>

using Blocks = std::map<std::wstring, PcmBlock>;

int main() {
    // The old mixer expands every source to the longest packet with zeros.
    // With stereo packets of 2 and 3 frames it inserts a false zero mic frame.
    const std::vector<short> mic{1000,1000,1100,1100};
    auto legacy = mic;
    legacy.resize(6, 0);
    assert(legacy[4] == 0 && legacy[3] == 1100);

    PcmBlockAligner aligner;
    auto first = aligner.Align({{L"mic", {mic, true}}, {L"game", {{0,0,0,0,0,0}, true}}}, 2);
    assert(first.at(L"mic") == mic); // NO invented zero at the boundary.
    auto second = aligner.Align({{L"mic", {{1200,1200,1300,1300,1400,1400}, true}}, {L"game", {{0,0,0,0}, true}}}, 2);
    assert((second.at(L"mic") == std::vector<short>{1200,1200,1300,1300,1400,1400}));
    assert(second.at(L"game").size() == 6);

    for (float gain : {1.f, .68f, .5f, .1f}) {
        aligner.Clear();
        std::vector<short> output;
        int nextSample = 1000;
        for (int i=0; i<2000; ++i) {
            const int frames = i%2 ? 5 : 4;
            std::vector<short> samples;
            for(int f=0; f<frames; ++f) { samples.push_back(nextSample); samples.push_back(nextSample++); }
            auto result = aligner.Align({{L"mic", {samples,true}}, {L"game", {std::vector<short>((i%2 ? 4:5)*2,0),true}}},2);
            const auto& part=result.at(L"mic"); output.insert(output.end(),part.begin(),part.end());
        }
        assert(output.size()==18000);
        for(size_t i=0; i<output.size();++i) {
            assert(output[i]==static_cast<short>(i/2+1000));
            const short scaled = static_cast<short>(std::lround(output[i]*gain));
            assert(scaled==static_cast<short>(std::lround((i/2+1000)*gain)));
            assert(scaled>0); // No invented zero notches after PCM16 gain conversion.
        }
    }
    aligner.Clear();
    auto silent = aligner.Align({{L"mic", {{4,4,5,5},true}}, {L"game", {{},false}}},2);
    assert((silent.at(L"mic")==std::vector<short>{4,4,5,5}));
    assert((silent.at(L"game")==std::vector<short>{0,0,0,0}));

    aligner.Clear();
    auto waiting = aligner.Align({{L"mic", {{10,10},true}}, {L"game", {{},true}}},2);
    assert(waiting.at(L"mic").empty());
    auto resumed = aligner.Align({{L"mic", {{11,11},true}}, {L"game", {{1,1},true}}},2);
    assert((resumed.at(L"mic")==std::vector<short>{10,10}));
    auto disconnected = aligner.Align({{L"mic", {{12,12},true}}},2);
    assert((disconnected.at(L"mic")==std::vector<short>{11,11,12,12}));
    aligner.Clear();
    assert((aligner.Align({{L"mic", {{42,42},true}}},2).at(L"mic")==std::vector<short>{42,42}));

    bool invalid=false, overflow=false;
    try { aligner.Align({{L"bad", {{1},true}}},2); } catch(const std::invalid_argument&) {invalid=true;}
    try { aligner.Align({{L"bad", {std::vector<short>(20,1),true}}},2,4); } catch(const std::runtime_error&) {overflow=true;}
    assert(invalid && overflow);
    std::cout << "PASS: unequal stereo blocks preserve PCM order/count; no zero notches at gains 1/.68/.5/.1; silence, underrun, disconnect, reset and bounds verified.\n";
}
