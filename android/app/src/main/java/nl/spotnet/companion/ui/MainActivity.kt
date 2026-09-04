package nl.spotnet.companion.ui

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.Bitmap
import android.os.Build
import android.os.Bundle
import android.webkit.*
import android.widget.Toast
import androidx.activity.addCallback
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import androidx.core.view.ViewCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.view.updatePadding
import nl.spotnet.companion.data.NotificationItem
import nl.spotnet.companion.data.NotificationSpot
import nl.spotnet.companion.data.PreferencesManager
import nl.spotnet.companion.databinding.ActivityMainBinding
import nl.spotnet.companion.notifications.NotificationHelper
import nl.spotnet.companion.notifications.SpotnetNotificationWorker
import org.json.JSONObject

class MainActivity : AppCompatActivity() {

    private lateinit var binding: ActivityMainBinding
    internal lateinit var prefs: PreferencesManager
    private var pendingNotificationOpen = false

    private val requestNotificationPermissionLauncher =
        registerForActivityResult(ActivityResultContracts.RequestPermission()) { isGranted ->
            if (isGranted) {
                Toast.makeText(this, "Meldingen ingeschakeld!", Toast.LENGTH_SHORT).show()
            }
        }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)

        // Apply window insets so content avoids the Android status bar and navigation bar
        ViewCompat.setOnApplyWindowInsetsListener(binding.root) { v, insets ->
            val statusBars = insets.getInsets(WindowInsetsCompat.Type.statusBars())
            val navBars = insets.getInsets(WindowInsetsCompat.Type.navigationBars())
            v.updatePadding(
                top = statusBars.top,
                bottom = navBars.bottom
            )
            insets
        }

        prefs = PreferencesManager(this)

        if (!prefs.isConnected) {
            startActivity(Intent(this, ConnectActivity::class.java))
            finish()
            return
        }

        checkNotificationPermission()
        setupWebView()

        if (intent.getStringExtra("EXTRA_NAV_TAB") == "notifications") {
            pendingNotificationOpen = true
        }

        // Schedule periodic background checks if enabled
        if (prefs.notificationsEnabled) {
            SpotnetNotificationWorker.schedule(this, prefs.notificationIntervalMinutes)
        }

        // Handle hardware back button
        onBackPressedDispatcher.addCallback(this) {
            binding.webViewSpots.evaluateJavascript(
                "(function() { " +
                "  const m = document.getElementById('notifModal'); if (m && m.style.display !== 'none') { closeNotifModal(); return 'modal_closed'; } " +
                "  const d = document.getElementById('detailModal'); if (d && d.style.display !== 'none') { closeDetail(); return 'detail_closed'; } " +
                "  return 'none'; " +
                "})()"
            ) { result ->
                val res = result?.replace("\"", "") ?: "none"
                if (res == "modal_closed" || res == "detail_closed") {
                    // Modal was closed by javascript
                } else if (binding.webViewSpots.canGoBack()) {
                    binding.webViewSpots.goBack()
                } else {
                    finish()
                }
            }
        }
    }

    override fun onNewIntent(intent: Intent?) {
        super.onNewIntent(intent)
        setIntent(intent)
        if (intent?.getStringExtra("EXTRA_NAV_TAB") == "notifications") {
            openNotificationsInWeb()
        }
    }

    private fun checkNotificationPermission() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            if (ContextCompat.checkSelfPermission(
                    this,
                    Manifest.permission.POST_NOTIFICATIONS
                ) != PackageManager.PERMISSION_GRANTED
            ) {
                requestNotificationPermissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
            }
        }
    }

    private fun setupWebView() {
        val webView = binding.webViewSpots
        val settings = webView.settings
        settings.javaScriptEnabled = true
        settings.domStorageEnabled = true
        settings.useWideViewPort = true
        settings.loadWithOverviewMode = true
        settings.cacheMode = WebSettings.LOAD_DEFAULT

        webView.addJavascriptInterface(SpotnetNativeInterface(this), "SpotnetNative")

        webView.webViewClient = object : WebViewClient() {
            override fun onPageStarted(view: WebView?, url: String?, favicon: Bitmap?) {
                binding.progressWebLoading.visibility = android.view.View.VISIBLE
            }

            override fun onPageFinished(view: WebView?, url: String?) {
                binding.progressWebLoading.visibility = android.view.View.GONE
                binding.swipeRefreshSpots.isRefreshing = false

                // Inject auth token into web localStorage if paired
                if (prefs.deviceToken.isNotBlank()) {
                    val js = "try { localStorage.setItem('spotnet_device_token', '${prefs.deviceToken}'); } catch(e){}"
                    view?.evaluateJavascript(js, null)
                }

                if (pendingNotificationOpen) {
                    pendingNotificationOpen = false
                    openNotificationsInWeb()
                }
            }

            override fun shouldOverrideUrlLoading(view: WebView?, request: WebResourceRequest?): Boolean {
                val url = request?.url?.toString() ?: return false
                if (url.startsWith("http://") || url.startsWith("https://")) {
                    return false
                }
                return true
            }
        }

        binding.swipeRefreshSpots.setOnRefreshListener {
            webView.reload()
        }

        // Only allow swipe-refresh when scrolled to top
        webView.viewTreeObserver.addOnScrollChangedListener {
            binding.swipeRefreshSpots.isEnabled = (webView.scrollY == 0)
        }

        loadWebCompanion()
    }

    private fun loadWebCompanion() {
        val url = if (prefs.deviceToken.isNotBlank()) {
            "${prefs.baseUrl}/?token=${prefs.deviceToken}"
        } else {
            prefs.baseUrl
        }
        binding.webViewSpots.loadUrl(url)
    }

    fun openNotificationsInWeb() {
        binding.webViewSpots.evaluateJavascript(
            "if (typeof openNotifModal === 'function') openNotifModal();",
            null
        )
    }

    fun sendTestNotification() {
        val testItem = NotificationItem(
            id = "test_${System.currentTimeMillis()}",
            ruleId = "test",
            ruleName = "F1 Formule 1 (Test)",
            ruleType = "Trefwoord",
            title = "Testnotificatie Spotnet Companion",
            body = "Het meldingsysteem is succesvol gekoppeld met uw Android-telefoon!",
            spotCount = 1,
            timeAgo = "Zojuist",
            createdAtUtc = "",
            isRead = false,
            spots = listOf(
                NotificationSpot(
                    id = 1,
                    messageId = "test",
                    title = "Formule 1 GP Nederland 2026 1080p",
                    categoryName = "Sport",
                    formattedSize = "4.2 GB",
                    formattedDate = "Vandaag"
                )
            )
        )
        NotificationHelper.showNotification(this, testItem)
        Toast.makeText(this, "Testnotificatie verzonden!", Toast.LENGTH_SHORT).show()
    }

    fun disconnectDevice() {
        prefs.disconnect()
        SpotnetNotificationWorker.cancel(this)
        val intent = Intent(this, ConnectActivity::class.java).apply {
            putExtra("EXTRA_FORCE_CONNECT", true)
            flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TASK
        }
        startActivity(intent)
        finish()
    }
}

class SpotnetNativeInterface(private val activity: MainActivity) {
    @JavascriptInterface
    fun isNativeApp(): Boolean = true

    @JavascriptInterface
    fun getNotificationSettings(): String {
        val json = JSONObject()
        json.put("notificationsEnabled", activity.prefs.notificationsEnabled)
        json.put("soundEnabled", activity.prefs.soundEnabled)
        json.put("vibrationEnabled", activity.prefs.vibrationEnabled)
        return json.toString()
    }

    @JavascriptInterface
    fun setNotificationSetting(key: String, value: Boolean) {
        activity.runOnUiThread {
            when (key) {
                "notificationsEnabled" -> {
                    activity.prefs.notificationsEnabled = value
                    if (value) {
                        SpotnetNotificationWorker.schedule(activity, activity.prefs.notificationIntervalMinutes)
                    } else {
                        SpotnetNotificationWorker.cancel(activity)
                    }
                }
                "soundEnabled" -> activity.prefs.soundEnabled = value
                "vibrationEnabled" -> activity.prefs.vibrationEnabled = value
            }
        }
    }

    @JavascriptInterface
    fun triggerTestNotification() {
        activity.runOnUiThread {
            activity.sendTestNotification()
        }
    }

    @JavascriptInterface
    fun disconnect() {
        activity.runOnUiThread {
            activity.disconnectDevice()
        }
    }
}