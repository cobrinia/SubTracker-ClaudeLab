---
name: keycloak-auth
description: Діагностика 401/403 у SubTracker.Api з Keycloak (JWT, ролі, політики RegisteredUser/AdminOnly, resource-based handlers). Використовуй, коли запит із токеном отримує 401 або 403, Scalar/Postman не авторизується.
---

1. Keycloak запущений? http://localhost:8080/realms/subtracker/.well-known/openid-configuration (scripts\keycloak-start.cmd).
2. Запит іде на http://localhost:5080 (профіль http)? Через https-редирект клієнт губить Authorization.
3. Розкодуй JWT (payload base64): aud містить subtracker-api, є claim roles, exp не минув.
4. 401 — проблема з токеном (немає, прострочений, не той aud/issuer). Логи: рядки JwtBearer у api-*.log.
5. 403 — токен валідний, але немає прав:
   - ендпоінт з політикою AdminOnly (Security/AuthorizationSetup.cs) → потрібна роль admin;
   - чужий ресурс → SubscriptionAuthorizationHandler / OwnedResourceAuthorizationHandler — це ОЧІКУВАНО (maria → підписка oleksandr).
6. Новий користувач Keycloak: JIT-прив'язка Users.ExternalId = sub, спершу пошук за e-mail (Security/CurrentUserService.cs).
7. Не виводь паролі й повні токени у відповідях.
