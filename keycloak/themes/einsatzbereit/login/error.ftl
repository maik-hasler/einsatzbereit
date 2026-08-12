<#import "template.ftl" as layout>
<#--
	Reached by an expired or already-used action token (the link in a
	verification or password-reset mail), a malformed authorization request, or
	any unrecoverable failure in a flow.

	Two things were wrong with it beyond the styling: base offers a way out
	only when ${client.baseUrl} is set, and this realm's frontend client has no
	baseUrl - so the single most likely page for a stuck visitor to land on was
	also the one with nothing to click. And an expired link is not really an
	error the visitor caused, so the page now says what to do next rather than
	only what went wrong.
-->
<#-- No eyebrow. Every other page uses one to name the step you are on, but
here the title already is that label - "Something went wrong" under an eyebrow
reading the same thing is the heading printed twice. -->
<@layout.registrationLayout
	displayMessage=false
	pageTitle="errorTitle"; section>

	<#if section = "header">
		${kcSanitize(msg("errorTitle"))?no_esc}

	<#elseif section = "form">
		<div id="kc-error-message">
			<p class="instruction">${kcSanitize(message.summary)?no_esc}</p>

			<#if traceId??>
				<p class="instruction" id="traceId">${msg("traceIdSupportMessage", traceId)}</p>
			</#if>

			<#if !skipLink??>
				<#if client?? && (client.baseUrl)?has_content>
					<#assign backUrl = client.baseUrl>
				<#else>
					<#assign backUrl = properties.siteUrl>
				</#if>
				<div class="${properties.kcFormButtonsClass!}">
					<a id="backToApplication" class="${properties.kcButtonClass!} ${properties.kcButtonPrimaryClass!} ${properties.kcButtonBlockClass!} ${properties.kcButtonLargeClass!}" href="${backUrl}">${msg("backToApplication")}</a>
				</div>
			</#if>
		</div>
	</#if>

</@layout.registrationLayout>
