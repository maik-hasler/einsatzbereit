<#import "template.ftl" as layout>
<#--
	Keycloak's generic "here is what just happened" page - email verified,
	already signed in, action completed. `message.summary` is the whole point
	of the page, and it used to render as an unstyled paragraph.

	The heading is the message itself when Keycloak supplies no dedicated
	header key, exactly as base does; the eyebrow is what keeps the page from
	arriving with no context at all.
-->
<@layout.registrationLayout
	displayMessage=false
	pageTitle="infoTitle"
	eyebrow="stepStatus"; section>

	<#if section = "header">
		<#if messageHeader??>
			${kcSanitize(msg("${messageHeader}"))?no_esc}
		<#else>
			${kcSanitize(message.summary)?no_esc}
		</#if>

	<#elseif section = "form">
		<div id="kc-info-message">
			<#-- Suppressed when it would only repeat the heading above it, which
			is the common case: with no messageHeader, base prints message.summary
			as both the title and the body. -->
			<#if messageHeader?? || (requiredActions??)>
				<p class="instruction">
					${kcSanitize(message.summary)?no_esc}<#if requiredActions??><#list requiredActions>: <b><#items as reqActionItem>${kcSanitize(msg("requiredAction.${reqActionItem}"))?no_esc}<#sep>, </#items></b></#list></#if>
				</p>
			</#if>

			<#if !skipLink??>
				<div class="${properties.kcFormButtonsClass!}">
					<#if pageRedirectUri?has_content>
						<a class="${properties.kcButtonClass!} ${properties.kcButtonPrimaryClass!} ${properties.kcButtonBlockClass!} ${properties.kcButtonLargeClass!}" href="${pageRedirectUri}">${msg("backToApplication")}</a>
					<#elseif actionUri?has_content>
						<a class="${properties.kcButtonClass!} ${properties.kcButtonPrimaryClass!} ${properties.kcButtonBlockClass!} ${properties.kcButtonLargeClass!}" href="${actionUri}">${msg("proceedWithAction")}</a>
					<#else>
						<#-- base falls back to client.baseUrl and renders nothing at
						all when it is empty - which it always is for this realm's
						frontend client, so the page ended here with nothing to
						click. -->
						<#if (client.baseUrl)?has_content>
							<#assign backUrl = client.baseUrl>
						<#else>
							<#assign backUrl = properties.siteUrl>
						</#if>
						<a class="${properties.kcButtonClass!} ${properties.kcButtonPrimaryClass!} ${properties.kcButtonBlockClass!} ${properties.kcButtonLargeClass!}" href="${backUrl}">${msg("backToApplication")}</a>
					</#if>
				</div>
			</#if>
		</div>
	</#if>

</@layout.registrationLayout>
