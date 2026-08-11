<#import "template.ftl" as layout>
<@layout.registrationLayout
	displayMessage=!messagesPerField.existsError("username","password")
	displayInfo=realm.password && realm.registrationAllowed && !registrationDisabled??
	pageTitle="loginTitle"
	eyebrow="stepSignIn"; section>

	<#if section = "header">
		${msg("loginTitle")}

	<#elseif section = "form">
		<#if realm.password>
			<#-- No positive tabindex values anywhere in this theme. They used to
			run 2,3,5,6,7,8 here, which puts every one of these controls ahead of
			every element without a tabindex - including the language switcher and
			the logo above them - regardless of where they sit in the document. DOM
			order is already the order a person reads the form in. -->
			<form id="kc-form-login" class="${properties.kcFormClass!}" action="${url.loginAction}" method="post">

				<div class="form-group">
					<div class="form-field">
						<input
							id="username"
							class="${properties.kcInputClass!}"
							name="username"
							value="${(login.username!'')}"
							type="text"
							aria-invalid="<#if messagesPerField.existsError('username','password')>true</#if>"
							autocomplete="username"
							autofocus
							required
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
					<#-- One combined message for both fields, deliberately: naming
					which of the two was wrong tells an attacker whether the account
					exists. The `required` attributes above are what stop an empty
					password from reaching the server and coming back as this
					message pointed at the username field. -->
					<#if messagesPerField.existsError('username','password')>
						<span id="input-error" class="${properties.kcInputErrorMessageClass!}" aria-live="polite">
							${kcSanitize(messagesPerField.getFirstError('username','password'))?no_esc}
						</span>
					</#if>
				</div>

				<div class="form-group">
					<div class="form-field form-field--with-toggle">
						<input
							id="password"
							class="${properties.kcInputClass!}"
							name="password"
							type="password"
							aria-invalid="<#if messagesPerField.existsError('username','password')>true</#if>"
							autocomplete="current-password"
							required
							placeholder=" "
						/>
						<label for="password" class="${properties.kcLabelClass!}">${msg("password")}</label>
						<#if properties.kcFormPasswordVisibilityButtonClass?has_content>
							<button
								class="${properties.kcFormPasswordVisibilityButtonClass!}"
								type="button"
								aria-label="${msg('showPassword')}"
								aria-controls="password"
								data-password-toggle
								data-icon-show="${properties.kcFormPasswordVisibilityIconShow!}"
								data-icon-hide="${properties.kcFormPasswordVisibilityIconHide!}"
								data-label-show="${msg('showPassword')}"
								data-label-hide="${msg('hidePassword')}"
							>
								<i class="${properties.kcFormPasswordVisibilityIconShow!}" aria-hidden="true"></i>
							</button>
						</#if>
					</div>
				</div>

				<div id="kc-form-options" class="form-options">
					<div class="form-options-wrapper">
						<#if realm.rememberMe>
							<label>
								<input id="rememberMe" name="rememberMe" type="checkbox" <#if login.rememberMe??>checked</#if>>
								<span>${msg("rememberMe")}</span>
							</label>
						</#if>
					</div>
					<#if realm.resetPasswordAllowed>
						<a href="${url.loginResetCredentialsUrl}">${msg("doForgotPassword")}</a>
					</#if>
				</div>

				<div id="kc-form-buttons" class="${properties.kcFormButtonsClass!}">
					<input
						class="${properties.kcButtonClass!} ${properties.kcButtonPrimaryClass!} ${properties.kcButtonBlockClass!} ${properties.kcButtonLargeClass!}"
						name="login"
						id="kc-login"
						type="submit"
						value="${msg('doLogIn')}"
					/>
				</div>

			</form>
		</#if>

	<#elseif section = "info">
		<#if realm.password && realm.registrationAllowed && !registrationDisabled??>
			${msg("noAccount")} <a href="${url.registrationUrl}">${msg("doRegister")}</a>
		</#if>
	</#if>

</@layout.registrationLayout>
