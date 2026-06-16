<#import "template.ftl" as layout>
<@layout.registrationLayout
	displayMessage=!messagesPerField.existsError("username")
	displayInfo=true; section>

	<#if section = "header">
		${msg("emailForgotTitle")}

	<#elseif section = "form">
		<form id="kc-reset-password-form" class="${properties.kcFormClass!}" action="${url.loginAction}" method="post">

			<div class="form-group">
				<div class="form-field">
					<input
						tabindex="1"
						id="username"
						class="${properties.kcInputClass!}"
						name="username"
						value="${(auth.attemptedUsername!'')}"
						type="text"
						aria-invalid="<#if messagesPerField.existsError('username')>true</#if>"
						autocomplete="username"
						autofocus
						placeholder=" "
					/>
					<label for="username" class="${properties.kcLabelClass!}">
						<#if !realm.loginWithEmailAllowed>
							${msg("username")}
						<#elseif !realm.registrationEmailAsUsername>
							${msg("usernameOrEmail")}
						<#else>
							${msg("email")}
						</#if>
					</label>
				</div>
				<#if messagesPerField.existsError('username')>
					<span id="input-error-username" class="${properties.kcInputErrorMessageClass!}" aria-live="polite">
						${kcSanitize(messagesPerField.get('username'))?no_esc}
					</span>
				</#if>
			</div>

			<div class="${properties.kcFormButtonsClass!}">
				<input
					tabindex="4"
					class="${properties.kcButtonClass!} ${properties.kcButtonPrimaryClass!} ${properties.kcButtonBlockClass!} ${properties.kcButtonLargeClass!}"
					type="submit"
					value="${msg('doSubmit')}"
				/>
			</div>

		</form>

	<#elseif section = "info">
		<a href="${url.loginUrl}">&larr; ${msg("backToLogin")}</a>

	</#if>

</@layout.registrationLayout>
