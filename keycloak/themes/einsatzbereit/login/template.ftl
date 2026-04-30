<#macro registrationLayout bodyClass="" displayInfo=false displayMessage=true displayRequiredFields=false>
<!DOCTYPE html>
<html lang="${locale.current!'de'}" class="<#if locale.current?? && locale.current == 'de'>lang-de<#else>lang-en</#if>">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>${msg("loginTitle", realm.displayName!'Einsatzbereit')}</title>
  <link rel="icon" type="image/x-icon" href="${url.resourcesPath}/img/favicon.ico">
  <link rel="stylesheet" href="${url.resourcesPath}/css/einsatzbereit.css">
  <#if properties.stylesCommon?has_content>
    <#list properties.stylesCommon?split(' ') as style>
      <link rel="stylesheet" href="${url.resourcesCommonPath}/${style}">
    </#list>
  </#if>
</head>
<body>
<div class="auth-page">

  <div class="auth-bg" aria-hidden="true">
    <div class="auth-bg-grid"></div>
  </div>

  <div class="top-controls">
    <#if realm.internationalizationEnabled && locale.supported?has_content>
    <details class="lang-switcher">
      <summary class="lang-trigger">
        <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="10"/><path d="M12 2a14.5 14.5 0 0 0 0 20 14.5 14.5 0 0 0 0-20"/><path d="M2 12h20"/></svg>
        <span>${locale.current?upper_case?substring(0, 2)}</span>
      </summary>
      <ul class="lang-menu" role="menu">
        <#list locale.supported as l>
          <li role="none"><a href="${l.url}" class="lang-item" role="menuitem">${l.label}</a></li>
        </#list>
      </ul>
    </details>
    </#if>
  </div>

  <main class="auth-main">
    <div class="auth-card">

      <div class="auth-brand">
        <span class="auth-brand-name">Einsatzbereit</span>
      </div>

      <div class="card-header">
        <h1 class="card-title"><#nested "header"></h1>
        <#if displayRequiredFields>
          <p class="card-subtitle">${msg("requiredFields")}</p>
        </#if>
      </div>

      <#if displayMessage && message?has_content && (message.type != 'warning' || !isAppInitiatedAction??)>
      <div class="alert alert-${message.type}" role="alert">
        <#if message.type == 'success'>
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>
        <#elseif message.type == 'warning'>
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z"/><path d="M12 9v4"/><path d="M12 17h.01"/></svg>
        <#elseif message.type == 'error'>
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="10"/><path d="m15 9-6 6"/><path d="m9 9 6 6"/></svg>
        <#else>
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="10"/><path d="M12 16v-4"/><path d="M12 8h.01"/></svg>
        </#if>
        <span>${kcSanitize(message.summary)?no_esc}</span>
      </div>
      </#if>

      <div class="card-body">
        <#nested "form">
      </div>

      <#if displayInfo>
      <div class="card-footer">
        <#nested "info">
      </div>
      </#if>

    </div>
  </main>

</div>

<#if properties.scripts?has_content>
  <#list properties.scripts?split(' ') as script>
    <script src="${url.resourcesPath}/${script}" type="text/javascript"></script>
  </#list>
</#if>
</body>
</html>
</#macro>
