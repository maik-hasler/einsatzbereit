<#import "template.ftl" as layout>
<@layout.registrationLayout
	displayMessage=!messagesPerField.existsError("email","username","password","password-confirm")
	displayRequiredFields=true
	displayInfo=true
	pageTitle="registerTitle"; section>

	<#if section = "header">
		${msg("registerTitle")}

	<#elseif section = "form">
		<form id="kc-register-form" class="${properties.kcFormClass!}" action="${url.registrationAction}" method="post">

			<div class="form-group">
				<div class="form-field">
					<input
						type="email"
						id="email"
						class="${properties.kcInputClass!}"
						name="email"
						value="${(register.formData.email!'')}"
						aria-invalid="<#if messagesPerField.existsError('email')>true</#if>"
						autocomplete="email"
						placeholder=" "
					/>
					<label for="email" class="${properties.kcLabelClass!}">${msg("email")}<span class="required">&#42;</span></label>
				</div>
				<#if messagesPerField.existsError('email')>
					<span id="input-error-email" class="${properties.kcInputErrorMessageClass!}" aria-live="polite">
						${kcSanitize(messagesPerField.get('email'))?no_esc}
					</span>
				</#if>
			</div>

			<#if !realm.registrationEmailAsUsername>
				<div class="form-group">
					<div class="form-field">
						<input
							type="text"
							id="username"
							class="${properties.kcInputClass!}"
							name="username"
							value="${(register.formData.username!'')}"
							aria-invalid="<#if messagesPerField.existsError('username')>true</#if>"
							autocomplete="username"
							placeholder=" "
						/>
						<label for="username" class="${properties.kcLabelClass!}">${msg("username")}<span class="required">&#42;</span></label>
					</div>
					<#if messagesPerField.existsError('username')>
						<span id="input-error-username" class="${properties.kcInputErrorMessageClass!}" aria-live="polite">
							${kcSanitize(messagesPerField.get('username'))?no_esc}
						</span>
					</#if>
				</div>
			</#if>

			<#if passwordRequired??>
				<div class="form-group">
					<div class="form-field form-field--with-toggle">
						<input
							type="password"
							id="password"
							class="${properties.kcInputClass!}"
							name="password"
							aria-invalid="<#if messagesPerField.existsError('password','password-confirm')>true</#if>"
							autocomplete="new-password"
							placeholder=" "
						/>
						<label for="password" class="${properties.kcLabelClass!}">${msg("password")}<span class="required">&#42;</span></label>
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
					<#if messagesPerField.existsError('password')>
						<span id="input-error-password" class="${properties.kcInputErrorMessageClass!}" aria-live="polite">
							${kcSanitize(messagesPerField.get('password'))?no_esc}
						</span>
					</#if>
				</div>

				<div class="form-group">
					<div class="form-field form-field--with-toggle">
						<input
							type="password"
							id="password-confirm"
							class="${properties.kcInputClass!}"
							name="password-confirm"
							aria-invalid="<#if messagesPerField.existsError('password-confirm')>true</#if>"
							autocomplete="new-password"
							placeholder=" "
						/>
						<label for="password-confirm" class="${properties.kcLabelClass!}">${msg("passwordConfirm")}<span class="required">&#42;</span></label>
						<#if properties.kcFormPasswordVisibilityButtonClass?has_content>
							<button
								class="${properties.kcFormPasswordVisibilityButtonClass!}"
								type="button"
								aria-label="${msg('showPasswordConfirm')}"
								aria-controls="password-confirm"
								data-password-toggle
								data-icon-show="${properties.kcFormPasswordVisibilityIconShow!}"
								data-icon-hide="${properties.kcFormPasswordVisibilityIconHide!}"
							>
								<i class="${properties.kcFormPasswordVisibilityIconShow!}" aria-hidden="true"></i>
							</button>
						</#if>
					</div>
					<#if messagesPerField.existsError('password-confirm')>
						<span id="input-error-password-confirm" class="${properties.kcInputErrorMessageClass!}" aria-live="polite">
							${kcSanitize(messagesPerField.get('password-confirm'))?no_esc}
						</span>
					</#if>
				</div>
			</#if>

			<div class="form-group">
				<div id="kc-registration-terms-text">
					${kcSanitize(msg("termsText"))?no_esc}
				</div>
			</div>
			<div class="form-group">
				<div class="form-field">
					<input
						type="checkbox"
						id="termsAccepted"
						name="termsAccepted"
						aria-invalid="<#if messagesPerField.existsError('termsAccepted')>true</#if>"
					/>
					<label for="termsAccepted">${msg("acceptTerms")}</label>
				</div>
				<#if messagesPerField.existsError('termsAccepted')>
					<span id="input-error-termsAccepted" class="${properties.kcInputErrorMessageClass!}" aria-live="polite">
						${kcSanitize(messagesPerField.get('termsAccepted'))?no_esc}
					</span>
				</#if>
			</div>

			<div class="${properties.kcFormButtonsClass!}">
				<input
					class="${properties.kcButtonClass!} ${properties.kcButtonPrimaryClass!} ${properties.kcButtonBlockClass!} ${properties.kcButtonLargeClass!}"
					type="submit"
					value="${msg('doRegister')}"
				/>
			</div>

		</form>

	<#elseif section = "info">
		<span>${msg("alreadyHaveAccount")} <a tabindex="6" href="${url.loginUrl}">${msg("doLogIn")}</a></span>

	</#if>
</@layout.registrationLayout>
