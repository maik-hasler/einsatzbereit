<#--
	Shared shell for every page in this theme.

	`eyebrow` and `lead` are message keys, not markup: each page names the step
	of the funnel it belongs to and, where the step is not self-explanatory,
	says in one line what is about to happen. They are macro parameters rather
	than <#nested> sections so that a template which has nothing to add simply
	omits them, and so the strings stay in the message bundles with everything
	else that gets translated.
-->
<#macro registrationLayout bodyClass="" displayInfo=false displayMessage=true displayRequiredFields=false pageTitle="loginTitle" eyebrow="" lead="">
<!DOCTYPE html>
<html lang="${locale.currentLanguageTag!'de'}">
<head>
	<meta charset="UTF-8">
	<meta name="viewport" content="width=device-width, initial-scale=1.0">
	<meta name="color-scheme" content="light">
	<#-- Every page used to inherit login.ftl's default pageTitle, so the browser
	tab read "Anmelden" while the page itself said "Es ist ein Fehler
	aufgetreten" - and a visitor with the reset-password and sign-in pages open
	side by side had two identical tabs. Each template passes its own now. -->
	<title>${msg(pageTitle, realm.displayName!'Einsatzbereit')} - Einsatzbereit</title>
	<link rel="icon" type="image/svg+xml" href="${url.resourcesPath}/img/favicon.svg">
	<link rel="stylesheet" href="${url.resourcesPath}/css/einsatzbereit.css">
</head>
<body>
<div class="auth-page">

	<div class="top-controls">
		<#if realm.internationalizationEnabled && locale.supported?has_content>
		<details class="lang-switcher">
			<summary class="lang-trigger">
				<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="10"/><path d="M12 2a14.5 14.5 0 0 0 0 20 14.5 14.5 0 0 0 0-20"/><path d="M2 12h20"/></svg>
				<span>${(locale.currentLanguageTag!'de')?upper_case}</span>
				<svg class="lang-chevron" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m6 9 6 6 6-6"/></svg>
			</summary>
			<ul class="lang-menu">
				<#list locale.supported as l>
					<li><a href="${l.url}" class="lang-item"<#if (l.locale!'') == (locale.currentLanguageTag!'')> aria-current="true"</#if>>${l.label}</a></li>
				</#list>
			</ul>
		</details>
		</#if>
	</div>

	<main class="auth-main">
		<div class="auth-card">

			<#-- The logo is the one control present on every page in this theme,
			so it carries the way back to the product rather than sitting there
			as decoration. -->
			<a class="auth-brand" href="${properties.siteUrl}">
				<img src="${url.resourcesPath}/img/logo.svg" alt="Einsatzbereit" class="auth-logo">
			</a>

			<div class="card-header">
				<#if eyebrow?has_content>
					<p class="card-eyebrow">${msg(eyebrow)}</p>
				</#if>
				<h1 class="card-title"><#nested "header"></h1>
				<#if lead?has_content>
					<p class="card-lead">${msg(lead)}</p>
				</#if>
				<#if displayRequiredFields>
					<p class="card-required">${msg("requiredFields")}</p>
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

		<#-- Way back to the product. Without it, a visitor who reaches these
		pages and changes their mind is stuck on an origin (login.*) that has
		no other link on it - browser Back is the only exit. -->
		<p class="auth-back">
			<a href="${properties.siteUrl}" class="back-link">
				<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
					stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
					<path d="m15 18-6-6 6-6"/>
				</svg>
				${msg("backToSite")}
			</a>
		</p>
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
