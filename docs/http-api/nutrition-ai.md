# 营养数据与 AI

[返回 HTTP API 目录](./README.md)。营养数据接口要求登录；两个 AI 接口额外要求 `Permission=Prescription` 声明。

## 能量与膳食参考值

| 方法与路径 | 请求体 | 成功响应 |
| --- | --- | --- |
| `POST /Energy/EERs/{gender}/{age}` | 特殊生理时期字符串数组，如 `[]` | 200，能量参考记录数组 |
| `POST /Energy/DRIs/{gender}/{age}` | 同上 | 200，膳食营养素参考记录数组 |

`gender` 为非空字符串，`age` 为非负十进制年龄（例如 `0.5`）；性别和特殊生理时期的字符串必须与站点参考数据相符。请求体是数组本身，不是包含数组的对象；无特殊时期发送 `[]`，也支持 JSON null。非法输入返回 400，无匹配参考数据返回 404 文本。

能量记录字段：`eerId`（整数）、`gender`、`ageStart`（十进制）、`pal`（十进制）、`avgBwEER`（整数）、`bee`（十进制）、`specialPhysiologicalPeriod`、`offsetEnergy`（十进制）。除 ID 外字段可为空。

膳食参考记录字段：`dietaryReferenceIntakeValueId`（整数）、`ageStart`、`gender`、`specialPhysiologicalPeriod`、`nutrient`、`recordType`、`isOffset`（布尔）、`value`（十进制）、`measureUnit`、`detail`。参考类型为 0 AI、1 RNI、2 EAR、3 UL、4 AMDR_L、5 AMDR_H、6 PI_NCD、7 SPL。

响应是按年龄与条件匹配后的参考记录，包含基础值和特殊时期偏移值；不能把每条记录直接当作最终个人推荐摄入量。`isOffset` 表示偏移记录，`measureUnit` 表示单位。

## 食物成分

| 方法与路径 | 成功响应 |
| --- | --- |
| `GET /FoodComposition/Foods` | 200，全部食物数组 |
| `GET /FoodComposition/Nutrients` | 200，全部营养素数组 |
| `GET /FoodComposition/CompositionData?friendlyCode=...` | 200，该食物的成分值数组 |

食物字段：`foodId`（GUID）、`friendlyCode`、`cite`、`ediblePortion`（可空整数）、`foodGroups`、`details`、`friendlyName`；其他描述字段也可为空。不包含内嵌成分列表。

营养素字段：`nutrientId`（整数）、`defaultMeasureUnit`、`details`、`friendlyName`，描述字段可为空。

成分值字段：`foodNutrientValueId`（整数）、`value`（十进制）、`measureUnit`、`details`、`foodId`、`food`、`nutrientId`、`nutrient`；嵌套对象使用上述食物与营养素结构。`friendlyCode` 必填且不能全为空白，错误返回 400；食物不存在返回 404，存在但没有成分记录可返回 `[]`。列表未分页。

## AI 环境与生成请求

`GET /Prescription/Environment` 返回 200 `{providerName,platformDetails,additionalInfo}`，用于展示供应商环境信息。

`POST /Prescription/Generate` 使用 JSON，最大请求体 1 MiB。最小请求示例：

```json
{
  "schemaVersion": 4,
  "patientInfo": { "gender": "女", "age": { "years": 30 } }
}
```

| 字段 | 契约 |
| --- | --- |
| `schemaVersion` | 当前为 4，其他版本不接受 |
| `patientInfo` | 必填对象；`age` 必填，`gender` 可选 |
| `patientInfo.age` | `years` 非负；`months` 可选 0–11，`days` 可选 0–30；提供 days 时必须提供 months |
| 其他个人数据 | 可选 `bmi`、`pal`、`height`、`weight`、整数 `totalBalanceEnergyViaCalculation`、`specialPhysiologicalPeriod` |
| `clinicalInfo` | 可选对象，包含字符串 `subjective`、`objective`、`assessment`、`plan` |
| `dietaryRecallSurvey` | 可选，结构如下 |

膳食回顾对象包含 `method`（默认 `24-hour-recall`）、`recallDays`（默认 1）、`foods` 与 `nutrients` 数组。食物项包含 `foodName`、`meal`、十进制 `edibleAmount`、`unit`。

营养素项包含 `name`、十进制 `intake`、`unit`、`referenceComparison`、`references`（`[{type,value,unit}]`），还可包含 `mealEnergyShares`（`[{meal,energy,percentageOfTotalEnergy}]`）和 `topFoodSources`（`[{foodName,amount,unit}]`）。

此请求中的枚举使用字符串：`meal` 为 `Breakfast`、`MorningSnack`、`Lunch`、`AfternoonSnack`、`Dinner`、`LateNightSnack`；`referenceComparison` 为 `NotEstablished`、`WithinReference`、`BelowReference`、`AboveReference`。

## SSE 响应

请求通过验证并开始处理后返回 200，`Content-Type: text/event-stream; charset=utf-8`，禁止缓存。响应不是一个完整 JSON 文档：

```text
: connected

data: {"Content":"思考片段","IsReasoningContent":true,"IsError":false}

data: {"Content":"建议片段","IsReasoningContent":false,"IsError":false}

data: [DONE]

```

注意 SSE 数据字段使用 **PascalCase**，与普通 HTTP JSON 的 camelCase 不同。每个事件以空行分隔，网络读取块不等于事件边界；忽略 `: connected` 等注释行。按 `IsReasoningContent` 区分推理与正文，`Content` 是增量片段，需要按接收顺序追加。

`IsError = true` 表示生成错误，`Content` 是错误文案。错误后仍可能收到 `[DONE]`，因此结束标记不等于业务成功。`[DONE]` 是字面标记，不是 JSON；未收到标记就断流应视为未完整结束。200 只表示流已开始，不能预先展示“生成成功”。

开始流之前的认证、授权、请求验证和服务器错误使用普通 HTTP 状态（例如 401、403、400、413、5xx）。流开始之后通过错误事件或断流表达失败，不能再依靠 HTTP 状态码判断。调用方可以取消连接，不能在已开始接收后自动重新生成；当前没有续传或恢复同一次生成的接口。
