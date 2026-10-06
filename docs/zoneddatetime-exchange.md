# 🔁 ZonedDateTime Exchange Codec (`NZDT/1`)

显式交换格式：精确 `Instant` 是唯一权威时刻；当地字符串**不**单独代表时刻。
zone ID、calendar ID、offset、provider 版本随值一起传输，全部作为解码时的显式校验。

## 📐 线格式

单行、`|` 分隔、`key=value` 字段，固定 5 个字段（顺序任意、不得重复、不得有多余字段）：

```
NZDT/1|i=2026-01-02T03:04:05.123456789Z|o=+02|z=Fake/Zone|c=ISO|v=v1
```

| 字段 | 含义 | 编码 |
|---|---|---|
| 头 | `NZDT/1` | 格式名 + 格式版本 |
| `i` | 精确瞬间（**权威**） | 扩展 ISO-8601 UTC，最多 9 位小数（纳秒） |
| `o` | 编码时该瞬间在 zone 内的 UTC offset | 不变文化 offset pattern（`+HH[:mm[:ss[.f…]]]`） |
| `z` | IANA/提供者 zone ID | 原样，不得含 `|` `=` 或换行 |
| `c` | Noda Time calendar ID（如 `ISO`、`Persian Simple`） | 原样 |
| `v` | 编码方 provider 的 `VersionId` | 原样 |

⚙️ 纳秒全程走整数/文本 pattern（`InstantPattern.ExtendedIso`），**不经过 double**，末位纳秒保留。

## ✅ 解码规则（严格，无静默回退）

1. 🚫 未知 zone → `DateTimeZoneNotFoundException`，**绝不回退本机时区**。
2. 🚫 未知 calendar → `ZonedDateTimeExchangeException`。
3. ⚔️ 声明 offset 与「瞬间 + zone」在当前数据下算出的 offset 不一致（且版本相同）→ 冲突异常，报出两个 offset。
4. 🏷️ 版本不一致 → 按策略处理，绝不静默吞掉：
   - `Reject`（默认）→ `ZonedDateTimeExchangeException`，报出两个版本；
   - `Reinterpret` → 以精确 instant 在新数据下重新解释，返回 `ZonedDateTimeExchangeReceipt`：
     `Status=Reinterpreted`，并记录 `DeclaredProviderVersion`/`ActualProviderVersion` 与
     `DeclaredOffset`/`ActualOffset`。
5. ✅ 全部通过 → `Status=Exact`。

重复当地时刻（如 Europe/London 2026-10-25 01:30 出现两次）因 `i`/`o` 不同而编码为两条不同记录，
两个 Instant 各自独立往返。

## 🧪 局部命令

```bash
# 构建核心库（netstandard2.0 + net8.0）
dotnet build src/NodaTime/NodaTime.csproj

# 仅运行本 codec 的测试（net10.0）
dotnet test src/NodaTime.Test/NodaTime.Test.csproj -f net10.0 \
  --filter "FullyQualifiedName~NodaTime.Test.Exchange"
```

## 🧩 API 一览（`NodaTime.Exchange`）

- `ZonedDateTimeExchangeCodec(provider, policy?)` — `Encode(ZonedDateTime)` / `Decode(string)` / `DecodeValue(string)`
- `ZonedDateTimeExchangeReceipt` — `Value`、`Status`、`Declared/ActualProviderVersion`、`Declared/ActualOffset`
- `ExchangeVersionMismatchPolicy` — `Reject`（默认）/ `Reinterpret`
- `ZonedDateTimeExchangeStatus` — `Exact` / `Reinterpreted`
- `ZonedDateTimeExchangeException`

原 `IPattern<T>`/`ZonedDateTimePattern` 等 pattern 接口未改动。
