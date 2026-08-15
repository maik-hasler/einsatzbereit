<#import "template.ftl" as layout>
<#--
	The confirmation the app sends people to when it signs them out without an
	id_token_hint.

	Base pairs the "Sign out" button with a cancel link that only renders when
	${client.baseUrl} is set - empty for this realm's frontend client - so the
	page offered exactly one action, and it was the irreversible one. A
	confirmation dialog with no way to say no is not a confirmation.
-->
<#-- Computed ahead of the macro call (rather than inside the "form" section
below) so it can also decide showBackLink=false: when this page renders its
own Cancel link, template.ftl's generic "Back to Einsatzbereit" safety net
would just be a second, differently-worded control for the exact same
destination and effect (#1931). -->
<#if logoutConfirm.skipLink>
	<#assign cancelUrl = "">
<#elseif (client.baseUrl)?has_content>
	<#assign cancelUrl = client.baseUrl>
<#else>
	<#assign cancelUrl = properties.siteUrl>
</#if>

<@layout.registrationLayout
	pageTitle="logoutConfirmTitle"
	eyebrow="stepSession"
	showBackLink=!cancelUrl?has_content; section>

	<#if section = "header">
		${msg("logoutConfirmTitle")}

	<#elseif section = "form">
		<div id="kc-logout-confirm" class="content-area">
			<p class="instruction">${msg("logoutConfirmHeader")}</p>

			<form class="form-actions" action="${url.logoutConfirmAction}" method="POST">
				<input type="hidden" name="session_code" value="${logoutConfirm.code}">
				<div id="kc-form-buttons" class="${properties.kcFormButtonsClass!}">
					<input
						class="${properties.kcButtonClass!} ${properties.kcButtonPrimaryClass!} ${properties.kcButtonBlockClass!} ${properties.kcButtonLargeClass!}"
						name="confirmLogout"
						id="kc-logout"
						type="submit"
						value="${msg('doLogout')}"
					/>
					<#if cancelUrl?has_content>
						<a
							id="kc-logout-cancel"
							class="${properties.kcButtonClass!} ${properties.kcButtonDefaultClass!} ${properties.kcButtonBlockClass!} ${properties.kcButtonLargeClass!}"
							href="${cancelUrl}"
						>${msg("doCancel")}</a>
					</#if>
				</div>
			</form>
		</div>
	</#if>

</@layout.registrationLayout>
