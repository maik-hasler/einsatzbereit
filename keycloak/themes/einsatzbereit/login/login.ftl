<#import "template.ftl" as layout>
<@layout.registrationLayout
	displayMessage=!messagesPerField.existsError("username","password")
	displayInfo=realm.password && realm.registrationAllowed && !registrationDisabled??; section>

	<#if section = "header">
		${msg("loginTitle")}

	<#elseif section = "form">
		<#if realm.password>
			<form id="kc-form-login" class="${properties.kcFormClass!}" action="${url.loginAction}" method="post">

				<#-- Username / Email -->
				<div class="${properties.kcFormGroupClass!}">
					<div class="${properties.kcLabelWrapperClass!}">
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
					<div class="${properties.kcInputWrapperClass!}">
						<input
							tabindex="2"
							id="username"
							class="${properties.kcInputClass!}"
							name="username"
							value="${(login.username!'')}"
							type="text"
							aria-invalid="<#if messagesPerField.existsError('username','password')>true</#if>"
							autocomplete="username"
							autofocus
						/>
						<#if messagesPerField.existsError('username','password')>
							<span id="input-error" class="${properties.kcInputErrorMessageClass!}" aria-live="polite">
								${kcSanitize(messagesPerField.getFirstError('username','password'))?no_esc}
							</span>
						</#if>
					</div>
				</div>

				<#-- Password -->
				<div class="${properties.kcFormGroupClass!}">
					<div class="${properties.kcLabelWrapperClass!}">
						<label for="password" class="${properties.kcLabelClass!}">${msg("password")}</label>
					</div>
					<div class="${properties.kcInputWrapperClass!} ${properties.kcInputGroup!}">
						<input
							tabindex="3"
							id="password"
							class="${properties.kcInputClass!}"
							name="password"
							type="password"
							aria-invalid="<#if messagesPerField.existsError('username','password')>true</#if>"
							autocomplete="current-password"
						/>
						<#if properties.kcFormPasswordVisibilityButtonClass?has_content>
							<button
								class="${properties.kcFormPasswordVisibilityButtonClass!}"
								type="button"
								aria-label="${msg('showPassword')}"
								aria-controls="password"
								data-password-toggle
								data-icon-show="${properties.kcFormPasswordVisibilityIconShow!}"
								data-icon-hide="${properties.kcFormPasswordVisibilityIconHide!}"
							>
								<i class="${properties.kcFormPasswordVisibilityIconShow!}" aria-hidden="true"></i>
							</button>
						</#if>
					</div>
				</div>

				<#-- Remember me + forgot password -->
				<div id="kc-form-options" class="form-options">
					<div class="form-options-wrapper">
						<#if realm.rememberMe>
							<label>
								<input tabindex="5" id="rememberMe" name="rememberMe" type="checkbox" <#if login.rememberMe??>checked</#if>>
								<span>${msg("rememberMe")}</span>
							</label>
						</#if>
					</div>
					<#if realm.resetPasswordAllowed>
						<a tabindex="6" href="${url.loginResetCredentialsUrl}">${msg("doForgotPassword")}</a>
					</#if>
				</div>

				<#-- Submit button -->
				<div id="kc-form-buttons" class="${properties.kcFormButtonsClass!}">
					<input
						tabindex="7"
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
			${msg("noAccount")} <a tabindex="8" href="${url.registrationUrl}">${msg("doRegister")}</a>
		</#if>
	</#if>

</@layout.registrationLayout>
