# 开发文档

## 前后端对接

[HTTP API 2.2](./http-api/README.md) 是跨端接口契约的统一入口，描述请求、响应、鉴权、错误和调用顺序。覆盖当前 2.2 源码（包括维护清理分支），不代表所有部署环境均已上线。

## 实现与部署

- [项目与依赖边界](./project-architecture.md)
- [认证会话实现](./authentication-sessions.md)：会话持久化、客户端协调与部署。
- [数据库运行时配置](./database-settings.md)：存储、Options 生命周期与重载。
- [账号清理实现](./account-cleanup.md)：筛选、事务与取消。
- [认证审核实现](./certification-review.md)：并发更新、超时处理与页面编排。
- [每日维护任务](./maintenance-cleanup.md)：调度与后台任务。
- [档案架构](./archive-architecture.md)
- [WPF 宿主](./wpf-hybrid-host.md)
- [版本管理](./versioning.md)

接口路径、传输字段、状态码和跨请求的可观察语义统一维护在 HTTP 文档组。内部服务、数据库、UI 状态、线程及文件管理留在实现文档；两边通过链接引用，不重复维护接口表。
