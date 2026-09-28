import re
p = r"C:\Users\22589\Documents\GitHub\Another-World\Assets\_Game\Prefabs\Cards\Summon\Card00_New_2D.prefab"
s = open(p, encoding="utf-8", errors="replace").read()
# split into documents
docs = s.split("--- !u!")
for d in docs:
    if "m_text:" in d:
        name = re.search(r"m_Name: (\S+)", d)
        txt = re.search(r"m_text: (.*)", d)
        fs = re.search(r"m_fontSize: ([\d.]+)", d)
        print(name.group(1) if name else "?", "|", txt.group(1)[:60], "| size", fs.group(1) if fs else "?")
