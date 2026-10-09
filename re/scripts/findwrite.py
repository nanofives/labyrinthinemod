"""Find instructions writing to [reg + OFFSET] inside methods whose name matches a prefix.
usage: python findwrite.py <name-substring> <offset hex> [<offset hex>...]"""
import sys, pickle, os, struct, bisect
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
HERE = os.path.dirname(os.path.abspath(__file__))
names, _ = pickle.load(open(os.path.join(HERE, "..", "il2cpp-dump", "names.pickle"), "rb"))
starts = sorted(names)
GA = r"C:\Program Files (x86)\Steam\steamapps\common\Labyrinthine\GameAssembly.dll"
data = open(GA, "rb").read()
pe = struct.unpack_from("<I", data, 0x3C)[0]; nsec = struct.unpack_from("<H", data, pe + 6)[0]; optsz = struct.unpack_from("<H", data, pe + 20)[0]
secs = []
for i in range(nsec):
    o = pe + 24 + optsz + i * 40; vsz, va, rsz, raw = struct.unpack_from("<IIII", data, o + 8); secs.append((va, max(vsz, rsz), raw))
def off(rva):
    for va, sz, raw in secs:
        if va <= rva < va + sz: return rva - va + raw
sub = sys.argv[1]; offs = [f"+ {o.lower()}]" for o in sys.argv[2:]]
md = Cs(CS_ARCH_X86, CS_MODE_64)
for k, a in enumerate(starts):
    n = names[a]
    if sub not in n: continue
    end = starts[k + 1] if k + 1 < len(starts) else a + 0x2000
    size = min(end - a, 0x8000)
    for ins in md.disasm(data[off(a):off(a) + size], a):
        if ins.mnemonic in ("mov","add","sub","inc","dec","xor","imul","lea") and any(ins.op_str.split(",")[0].endswith(x) for x in offs):
            print(f"{n}  {ins.address:08x}  {ins.mnemonic} {ins.op_str}")
