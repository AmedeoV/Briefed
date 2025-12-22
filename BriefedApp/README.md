# Briefed Android App

This is a WebView-based Android application for Briefed that can be published to the Google Play Store.

## Project Structure

```
BriefedApp/
├── app/
│   ├── src/
│   │   └── main/
│   │       ├── java/com/briefed/app/
│   │       │   └── MainActivity.java
│   │       ├── res/
│   │       │   ├── layout/
│   │       │   │   └── activity_main.xml
│   │       │   ├── values/
│   │       │   │   ├── strings.xml
│   │       │   │   ├── colors.xml
│   │       │   │   └── themes.xml
│   │       │   ├── xml/
│   │       │   │   ├── backup_rules.xml
│   │       │   │   └── data_extraction_rules.xml
│   │       │   └── mipmap-*/
│   │       │       └── (app icons)
│   │       └── AndroidManifest.xml
│   ├── build.gradle
│   └── proguard-rules.pro
├── gradle/
│   └── wrapper/
│       └── gradle-wrapper.properties
├── build.gradle
├── gradle.properties
└── settings.gradle
```

## Setup Instructions

### 1. Configure Your App URL

Open `MainActivity.java` and update the `APP_URL` constant with your actual Briefed website URL:

```java
private static final String APP_URL = "https://yourbriefedapp.com";
```

### 2. Create App Icons

You need to create launcher icons for your app. Use one of these tools:

- **Icon Kitchen**: https://icon.kitchen/
- **Android Asset Studio**: https://romannurik.github.io/AndroidAssetStudio/

Generate icons for all densities:
- mdpi (48x48 px)
- hdpi (72x72 px)
- xhdpi (96x96 px)
- xxhdpi (144x144 px)
- xxxhdpi (192x192 px)

Place the generated icons in the respective `mipmap-*` folders.

### 3. Customize Branding

Edit the following files to match your brand:

- **colors.xml**: Update primary, accent colors
- **strings.xml**: Update app name if needed
- **themes.xml**: Customize theme colors

### 4. Build the App

#### Prerequisites:
- Install Android Studio
- Install JDK 11 or higher
- Set up Android SDK

#### Build Steps:

1. Open Android Studio
2. Select "Open an Existing Project"
3. Navigate to the `BriefedApp` folder
4. Wait for Gradle sync to complete
5. Build the app: **Build → Build Bundle(s) / APK(s) → Build APK(s)**

Or via command line:
```bash
cd BriefedApp
./gradlew assembleRelease
```

### 5. Test the App

- Run on emulator: Click the "Run" button in Android Studio
- Run on physical device: Enable USB debugging and connect your device

### 6. Prepare for Play Store

#### Create a Signing Key:

```bash
keytool -genkey -v -keystore briefed-release-key.jks -keyalg RSA -keysize 2048 -validity 10000 -alias briefed
```

#### Configure Signing in `app/build.gradle`:

Add this before the `buildTypes` block:

```gradle
signingConfigs {
    release {
        storeFile file("path/to/briefed-release-key.jks")
        storePassword "your-password"
        keyAlias "briefed"
        keyPassword "your-password"
    }
}
```

Update the release build type:

```gradle
buildTypes {
    release {
        signingConfig signingConfigs.release
        minifyEnabled true
        proguardFiles getDefaultProguardFile('proguard-android-optimize.txt'), 'proguard-rules.pro'
    }
}
```

#### Generate Signed Bundle:

```bash
./gradlew bundleRelease
```

The AAB file will be in: `app/build/outputs/bundle/release/app-release.aab`

### 7. Play Store Requirements

Before publishing, make sure you have:

1. **App Icon**: High-quality icon (512x512 px)
2. **Feature Graphic**: 1024x500 px banner
3. **Screenshots**: At least 2 screenshots (phone and/or tablet)
4. **Privacy Policy**: URL to your privacy policy
5. **App Description**: Short and full descriptions
6. **Content Rating**: Complete the questionnaire
7. **Target API Level**: Set to at least API 33 (Android 13)

### 8. Update Version

When releasing updates, increment version in `app/build.gradle`:

```gradle
versionCode 2  // Increment this
versionName "1.1"  // Update this
```

## Features

- Full WebView integration with JavaScript support
- Pull-to-refresh functionality
- Back button navigation support
- Network error handling
- Responsive design
- Cookie and local storage support
- Mixed content support (HTTP/HTTPS)

## Troubleshooting

### Clear Text Traffic Error
If your site uses HTTP (not recommended), the manifest already includes:
```xml
android:usesCleartextTraffic="true"
```

For production, always use HTTPS.

### WebView Not Loading
- Check internet permissions in manifest
- Verify the URL is correct
- Check network connectivity
- Review logcat for errors

### App Crashes
- Check logcat: `adb logcat | grep briefed`
- Verify all dependencies are properly synced
- Clean and rebuild: **Build → Clean Project** then **Build → Rebuild Project**

## Permissions

The app requires:
- `INTERNET`: To load web content
- `ACCESS_NETWORK_STATE`: To check connectivity

## License

Update this section with your license information.

## Support

For issues or questions, contact: [your-email@example.com]
