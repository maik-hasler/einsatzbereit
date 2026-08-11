<#import "template.ftl" as layout>
<#--
	Every new account passes through this page: the realm has verifyEmail on,
	so registration hands straight off to it. It was not overridden, so the
	first thing a new volunteer saw after signing up was base Keycloak markup -
	a bare <p class="instruction"> with no rule behind it - inside this theme's
	card.
-->
<@layout.registrationLayout
	displayInfo=!isAppInitiatedAction??
	pageTitle="emailVerifyTitle"
	eyebrow="stepVerify"; section>

	<#if section = "header">
		${msg("emailVerifyTitle")}

	<#elseif section = "form">
		<p class="instruction">
			<#if verifyEmail??>
				${msg("emailVerifyInstruction1", verifyEmail)}
			<#else>
				${msg("emailVerifyInstruction4", user.email)}
			</#if>
		</p>

		<#-- Only when the app itself asked for the verification (an
		application-initiated action); in the ordinary post-registration case
		Keycloak has already sent the mail and the resend link lives in the
		info section below. -->
		<#if isAppInitiatedAction??>
			<form id="kc-verify-email-form" class="${properties.kcFormClass!}" action="${url.loginAction}" method="post">
				<div id="kc-form-buttons" class="${properties.kcFormButtonsClass!}">
					<#if verifyEmail??>
						<input
							class="${properties.kcButtonClass!} ${properties.kcButtonPrimaryClass!} ${properties.kcButtonBlockClass!} ${properties.kcButtonLargeClass!}"
							type="submit"
							value="${msg('emailVerifyResend')}"
						/>
					<#else>
						<input
							class="${properties.kcButtonClass!} ${properties.kcButtonPrimaryClass!} ${properties.kcButtonBlockClass!} ${properties.kcButtonLargeClass!}"
							type="submit"
							value="${msg('emailVerifySend')}"
						/>
					</#if>
					<button
						class="${properties.kcButtonClass!} ${properties.kcButtonDefaultClass!} ${properties.kcButtonBlockClass!} ${properties.kcButtonLargeClass!}"
						type="submit"
						name="cancel-aia"
						value="true"
						formnovalidate
					>${msg("doCancel")}</button>
				</div>
			</form>
		</#if>

	<#elseif section = "info">
		<span>${msg("emailVerifyInstruction2")} <a href="${url.loginAction}">${msg("emailVerifyInstruction3")}</a></span>

	</#if>

</@layout.registrationLayout>
