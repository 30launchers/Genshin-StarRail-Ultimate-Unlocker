import re

# 黑名单地址集合（统一转为小写，便于比较）
blacklist = {
    "0x0619eb09", "0x093d6186", "0x0dc79bd2", "0x0eef6c1c",
    "0x08ce237a", "0x0accb056", "0x11238de5", "0x077167e9",
    "0x0ea67652", "0x06129253", "0x10c61ab2", "0x0ced6041",
    "0x08b1a2e5"
}

# 正则表达式匹配十六进制地址（如 0x1234ABCD）
pattern = re.compile(r'0x[0-9A-Fa-f]+')

try:
    with open('address.txt', 'r', encoding='utf-8') as f:
        for line in f:
            match = pattern.search(line)
            if match:
                addr = match.group(0).lower()  # 统一小写比较
                if addr not in blacklist:
                    print(addr)  # 打印未出现在黑名单中的地址
except FileNotFoundError:
    print("错误：找不到 address.txt 文件")