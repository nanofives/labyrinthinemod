"""Disassemble a GameAssembly.dll function by RVA, annotating calls with IL2CPP method names.
usage: python -I disasm.py <RVA hex> [max_bytes]"""
import sys, json, struct, os, pickle
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
GA = r"C:\Program Files (x86)\Steam\steamapps\common\Labyrinthine\GameAssembly.dll"
HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPT = os.path.join(HERE, "..", "il2cpp-dump", "script.json")
CACHE = os.path.join(HERE, "..", "il2cpp-dump", "names.pickle")

def load_names():
    if os.path.exists(CACHE):
        return pickle.load(open(CACHE, "rb"))
    d = json.load(open(SCRIPT, encoding="utf-8"))
    names = {m["Address"]: m["Name"] for m in d["ScriptMethod"]}
    meta = {m["Address"]: m["Name"] for m in d.get("ScriptMetadataMethod", [])}
    meta.update({m["Address"]: m["Name"] for m in d.get("ScriptMetadata", [])})
    pickle.dump((names, meta), open(CACHE, "wb"))
    return names, meta

data = open(GA, "rb").read()
pe = struct.unpack_from("<I", data, 0x3C)[0]
nsec = struct.unpack_from("<H", data, pe + 6)[0]
optsz = struct.unpack_from("<H", data, pe + 20)[0]
base = struct.unpack_from("<Q", data, pe + 24 + 24)[0]
secs = []
for i in range(nsec):
    o = pe + 24 + optsz + i * 40
    vsz, va, rsz, raw = struct.unpack_from("<IIII", data, o + 8)
    secs.append((va, max(vsz, rsz), raw))
def off(rva):
    for va, sz, raw in secs:
        if va <= rva < va + sz: return rva - va + raw
rva = int(sys.argv[1], 16); n = int(sys.argv[2], 0) if len(sys.argv) > 2 else 0x400
names, meta = load_names()
md = Cs(CS_ARCH_X86, CS_MODE_64)
for ins in md.disasm(data[off(rva):off(rva) + n], rva):
    note = ""
    if ins.mnemonic == "call" and ins.op_str.startswith("0x"):
        note = names.get(int(ins.op_str, 16), "")
    elif "rip +" in ins.op_str:
        tgt = ins.address + ins.size + int(ins.op_str.split("rip + ")[1].split("]")[0], 16)
        note = meta.get(tgt, "")
    print(f"{ins.address:08x}  {ins.mnemonic:6} {ins.op_str}  {('; ' + note) if note else ''}")
    if ins.mnemonic == "int3": break
