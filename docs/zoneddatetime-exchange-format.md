# 🔄 ZonedDateTime 交换格式 `NODAZDT/1`

`NodaTime.Text.ZonedDateTimeExchangeCodec` —— 显式、自描述的 `ZonedDateTime` 交换 codec。
与 `ZonedDateTimePattern`（接口保持不变）不同，payload 直接携带时刻本身，**不把当地字符串当作唯一时刻**。

## 📦 格式

单行、`|` 分隔的 `key=value` 字段，键固定、顺序固定、无转义（字段值不得含 `|`/换行，编码时校验）：

```
NODAZDT/1|i=<unix-seconds>.<nano-of-second>|o=<offset-seconds>|z=<zone-id>|c=<calendar-id>|v=<provider-version-id>|l=<local-iso>
```

| 键 | 含义 | 说明 |
|----|------|------|
| `i` | 精确 `Instant` | 整数 Unix 秒（floor，可负）+ 9 位纳秒。**全程整数，不经 double**，纳秒末位保留 |
| `o` | 声明的 UTC offset | 整数秒（如 `3600`），解码时与 zone 实际 offset 核对 |
| `z` | 时区 ID | 如 `Europe/Paris`；解码方 provider 必须认识，**未知 zone 报错，不回退本机时区** |
| `c` | 日历 ID | `CalendarSystem.Id`，如 `ISO`、`Hebrew Civil`；解码用 `CalendarSystem.ForId` |
| `v` | 提供者版本 | 编码方 `IDateTimeZoneProvider.VersionId`，如 `TZDB: 2026e` |
| `l` | 当地日期时间 | 不变文化 ISO-8601（纳秒），**仅作派生交叉校验，不是时刻来源** |

示例：

```
NODAZDT/1|i=1760000000.123456789|o=7200|z=Europe/Paris|c=ISO|v=TZDB: 2026e|l=2025-10-09T13:33:20.123456789
```

重复（模糊）当地时刻的两个 `Instant` 因 `i`/`o` 不同而产生不同 payload，可分别往返。

## ✅ 解码规则

1. 信封：格式标识、六个键齐全、无未知/重复键 —— 否则失败。
2. `i` 解析为 `Instant.FromUnixTimeSeconds(s).PlusNanoseconds(n)`，越界失败。
3. zone 必须可用（`GetZoneOrNull`，无本机回退）；calendar 必须可用 —— 否则失败。
4. **offset 一致性**：`o` 必须等于 `zone.GetUtcOffset(instant)`；不一致 = 数据冲突，显式失败，任何策略下都不可重新解释。
5. **版本策略**（`ZonedDateTimeExchangeVersionPolicy`）：
   - `Reject`（默认）：`v` 与解码 provider 版本不同 → 失败，错误含两个版本；
   - `ReinterpretWithReceipt`：按解码方数据重新解释并返回成功，但回执
     `ProviderVersionMismatch` / `EncodedProviderVersionId` / `DecodingProviderVersionId` / `Warnings`
     完整记录 —— **版本变化绝不静默吞掉**。
6. `l` 与由 `Instant`+zone+calendar 重算的当地时间逐字符比对，不一致 = payload 损坏，失败。

解码永不抛异常（非法策略枚举除外），结果通过 `ZonedDateTimeExchangeDecodeResult` 回执报告。

## 🛠️ dotnet 局部命令

```bash
# 缓存/临时目录定向到 D 盘
export DOTNET_ROOT='D:\Codex\dotnet' PATH="/d/Codex/dotnet:$PATH" \
  DOTNET_CLI_HOME='D:\Codex\dotnet-cli-home' NUGET_PACKAGES='D:\Codex\nuget-packages' \
  TEMP='D:\Codex\tmp-1007-122' TMP='D:\Codex\tmp-1007-122' \
  DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

dotnet build src/NodaTime/NodaTime.csproj                                  # 核心库
dotnet test src/NodaTime.Test/NodaTime.Test.csproj -f net10.0 \
  --filter "FullyQualifiedName~ZonedDateTimeExchangeCodecTest"             # 本特性验收
dotnet test src/NodaTime.Test/NodaTime.Test.csproj -f net10.0 \
  --filter "FullyQualifiedName~NodaTime.Test.Text"                         # pattern 回归
```

## 🎯 验收映射（`ZonedDateTimeExchangeCodecTest`）

| 验收点 | 测试 |
|--------|------|
| 重复当地时刻两个 Instant 分别往返 | `AmbiguousLocalTime_BothInstantsRoundTripSeparately` |
| fake provider 换 offset → 冲突明确 | `OffsetConflict_WhenProviderChangesOffset_IsExplicitFailure` |
| 纳秒末位保留（不经 double） | `Nanoseconds_UnitsDigitPreserved` |
| 版本变化不静默：拒绝 / 回执 | `ProviderVersionMismatch_RejectedByDefault` / `ProviderVersionMismatch_ReinterpretedWithReceipt_NeverSilent` |
| 未知 zone 不回退本机 | `UnknownZone_FailsWithoutLocalFallback` |
| 未知 calendar 失败 | `UnknownCalendar_Fails` |
| 当地字段被篡改失败 | `TamperedLocalTimeField_Fails` |
| 畸形 payload 失败 | `MalformedPayloads_Fail` |
| zone/calendar/版本完整往返 | `RoundTrip_PreservesInstantZoneCalendarAndProviderVersion` |
