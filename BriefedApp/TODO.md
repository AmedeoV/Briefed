# Briefed App - Important Notes

## TODO Before Publishing:

1. **Update App URL**: 
   - Open `app/src/main/java/com/briefed/app/MainActivity.java`
   - Change `APP_URL` from "https://yourbriefedapp.com" to your actual URL

2. **Add App Icons**:
   - Generate icons using https://icon.kitchen/ or similar
   - Replace placeholder icons in all mipmap-* folders
   - Need both ic_launcher.png and ic_launcher_foreground.png for each density

3. **Create Signing Key**:
   - Run: `keytool -genkey -v -keystore briefed-release-key.jks -keyalg RSA -keysize 2048 -validity 10000 -alias briefed`
   - Keep this file SECURE and BACKED UP!

4. **Customize Package Name** (Optional):
   - Current: `com.briefed.app`
   - Change in: build.gradle, AndroidManifest.xml, and folder structure if needed

5. **Test Thoroughly**:
   - Test on multiple devices and Android versions
   - Test all features: navigation, back button, pull-to-refresh
   - Test offline behavior
   - Test deep links (if applicable)

6. **Update Gradle Plugin** (if needed):
   - Current AGP version: 8.2.0
   - Check for updates at build time

7. **Privacy Policy**:
   - Required for Play Store
   - Must disclose data collection and usage

8. **Target SDK**:
   - Currently set to API 34 (Android 14)
   - Play Store requires targeting recent API levels
