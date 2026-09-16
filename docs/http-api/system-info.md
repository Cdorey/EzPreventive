# 公共信息

[返回 HTTP API 目录](./README.md)。以下接口均允许匿名访问，包括名称为“登录后公告”的 `Notice`。

| 方法与路径 | 成功响应 |
| --- | --- |
| `GET /SystemInfo/PublicInfo` | 200，`{caseNumber,serverVersion}` |
| `GET /SystemInfo/CoverLetter` | 200，最新登录前介绍 |
| `GET /SystemInfo/Notice` | 200，最新登录后公告 |
| `GET /SystemInfo/UserAgreement` | 200，最新用户协议 |
| `GET /SystemInfo/PrivacyPolicy` | 200，最新隐私政策 |

公共信息示例：

```json
{ "caseNumber": "示例备案号", "serverVersion": "2.2.0.0" }
```

备案号去除首尾空白；缺失信息可为 null。产品版本是四段数字，不带构建元数据。此组合接口替代独立备案号接口，不提供旧接口回退；版本兼容规则见[版本管理](../versioning.md)。

其余四个接口返回同一结构；没有对应类型的内容时返回 404，错误正文由框架处理，不应当作内容对象解析：

```json
{
  "noticeId": "11111111-1111-4111-8111-111111111111",
  "kind": 0,
  "title": "公告标题",
  "description": "公告正文",
  "publisherId": "publisher-user-id",
  "createTime": "2026-09-05T00:00:00Z"
}
```

`kind` 对应 0 登录后公告、1 登录前介绍、2 用户协议、3 隐私政策；按创建时间选取最新一条。发布见[站点管理](./administration.md#发布公告与政策文本)。
