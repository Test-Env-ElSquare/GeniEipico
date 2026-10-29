# Namespace Fixed Version

تم تعديل الـ namespaces والـ usings لتناسب أسماء المشاريع الموجودة عندك في Solution Explorer:

- `MesPlatform.Domain` => `Domain`
- `MesPlatform.Application` => `Application`
- `MesPlatform.Infrastructure` => `Infrastructure`
- `MesPlatform.API` => `API`

وكمان تم تغيير أسماء الفولدرات/الـ csproj داخل النسخة الجديدة لنفس أسماء الـ projects:
`Domain`, `Application`, `Infrastructure`, `API`.

نفس القيود محفوظة:
- CustomerOnly version
- No Tenant table
- No Signage
- No SignalR
- No Background Jobs
- No Patch methods; use Update
- SQL Server + Identity + DateTime + int Id
