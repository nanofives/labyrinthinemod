"""Find direct call sites (E8 rel32) to target RVAs in GameAssembly.dll and name the containing method.
usage: python xrefs.py <RVA hex> [<RVA hex> ...]"""
import bisect
import os
import pickle
import struct
import sys

import numpy as np

GA = r"C:\Program Files (x86)\Steam\steamapps\common\Labyrinthine\GameAssembly.dll"
HERE = os.path.dirname(os.path.abspath(__file__))
names, _ = pickle.load(open(os.path.join(HERE, "..", "il2cpp-dump", "names.pickle"), "rb"))
starts = sorted(names)

data = open(GA, "rb").read()
pe = struct.unpack_from("<I", data, 0x3C)[0]
nsec = struct.unpack_from("<H", data, pe + 6)[0]
optsz = struct.unpack_from("<H", data, pe + 20)[0]
code = []
for i in range(nsec):
    o = pe + 24 + optsz + i * 40
    vsz, va, rsz, raw = struct.unpack_from("<IIII", data, o + 8)
    if struct.unpack_from("<I", data, o + 36)[0] & 0x20000000:
        code.append((va, rsz, raw))

targets = {int(a, 16) for a in sys.argv[1:]}
hits = {t: [] for t in targets}
for va, rsz, raw in code:
    buf = np.frombuffer(data, dtype=np.uint8, count=rsz, offset=raw)
    idx = np.nonzero(buf[:-5] == 0xE8)[0]
    rel = (buf[idx + 1].astype(np.int64) | (buf[idx + 2].astype(np.int64) << 8)
           | (buf[idx + 3].astype(np.int64) << 16) | (buf[idx + 4].astype(np.int64) << 24))
    rel = np.where(rel >= 1 << 31, rel - (1 << 32), rel)
    dest = va + idx + 5 + rel
    for t in targets:
        for off in idx[dest == t]:
            hits[t].append(va + int(off))

for t in sorted(targets):
    print(f"== callers of {t:x} {names.get(t, '')}")
    for rva in hits[t]:
        k = bisect.bisect_right(starts, rva) - 1
        print(f"  {rva:08x} in {names[starts[k]]} (+{rva - starts[k]:x})")
