package nl.spotnet.companion.ui

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.Bitmap
import android.os.Build
import android.os.Bundle
import android.view.View
import android.webkit.*
import android.widget.Toast
import androidx.activity.addCallback
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import androidx.core.view.ViewCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.view.updatePadding
import androidx.lifecycle.lifecycleScope
import androidx.recyclerview.widget.LinearLayoutManager
import com.google.android.material.dialog.MaterialAlertDialogBuilder
import kotlinx.coroutines.launch
import nl.spotnet.companion.R
import nl.spotnet.companion.data.*
import nl.spotnet.companion.databinding.ActivityMainBinding
import nl.spotnet.companion.notifications.NotificationHelper
import nl.spotnet.companion.notifications.SpotnetNotificationWorker

class MainActivity : AppCompatActivity() {

    private lateinit var binding: ActivityMainBinding
    private lateinit var prefs: PreferencesManager
    private lateinit var apiClient: SpotnetApiClient
    private lateinit var notifAdapter: NotificationsAdapter

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

        // Apply window insets for status bar (notch / cutout) and bottom navigation
        ViewCompat.setOnApplyWindowInsetsListener(binding.appBarLayout) { v, insets ->
            val statusBars = insets.getInsets(WindowInsetsCompat.Type.statusBars())
            v.updatePadding(top = statusBars.top)
            insets
        }
        ViewCompat.setOnApplyWindowInsetsListener(binding.bottomNavigation) { v, insets ->
            val navBars = insets.getInsets(WindowInsetsCompat.Type.navigationBars())
            v.updatePadding(bottom = navBars.bottom)
            insets
        }

        prefs = PreferencesManager(this)
        apiClient = SpotnetApiClient(this)

        if (!prefs.isConnected) {
            startActivity(Intent(this, ConnectActivity::class.java))
            finish()
            return
        }

        checkNotificationPermission()
        setupToolbar()
        setupBottomNav()
        setupWebView()
        setupNotificationsView()
        setupSettingsView()

        // Handle intent extras (e.g. opened from notification)
        if (intent.getStringExtra("EXTRA_NAV_TAB") == "notifications") {
            binding.bottomNavigation.selectedItemId = R.id.nav_notifications
        }

        onBackPressedDispatcher.addCallback(this) {
            if (binding.tabViewSpots.visibility == View.VISIBLE && binding.webViewSpots.canGoBack()) {
                binding.webViewSpots.goBack()
            } else {
                finish()
            }
        }

        // Fetch initial status & notifications
        fetchStatus()
        fetchNotifications()
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

    private fun setupToolbar() {
        binding.toolbar.subtitle = "${prefs.serverHost}:${prefs.serverPort}"

        binding.btnSyncSpots.setOnClickListener {
            binding.btnSyncSpots.animate().rotationBy(360f).setDuration(800).start()
            Toast.makeText(this, "Nieuwe spots ophalen gestart op pc…", Toast.LENGTH_SHORT).show()
            lifecycleScope.launch {
                val res = apiClient.triggerSpotsSync()
                if (res.isSuccess) {
                    Toast.makeText(this@MainActivity, "✓ Usenet update gestart op pc!", Toast.LENGTH_SHORT).show()
                }
            }
        }
    }

    private fun setupBottomNav() {
        binding.bottomNavigation.setOnItemSelectedListener { item ->
            when (item.itemId) {
                R.id.nav_spots -> {
                    binding.tabViewSpots.visibility = View.VISIBLE
                    binding.tabViewNotifications.visibility = View.GONE
                    binding.tabViewSettings.visibility = View.GONE
                    true
                }
                R.id.nav_notifications -> {
                    binding.tabViewSpots.visibility = View.GONE
                    binding.tabViewNotifications.visibility = View.VISIBLE
                    binding.tabViewSettings.visibility = View.GONE
                    fetchNotifications()
                    true
                }
                R.id.nav_settings -> {
                    binding.tabViewSpots.visibility = View.GONE
                    binding.tabViewNotifications.visibility = View.GONE
                    binding.tabViewSettings.visibility = View.VISIBLE
                    fetchStatus()
                    true
                }
                else -> false
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

        webView.webViewClient = object : WebViewClient() {
            override fun onPageStarted(view: WebView?, url: String?, favicon: Bitmap?) {
                binding.progressWebLoading.visibility = View.VISIBLE
            }

            override fun onPageFinished(view: WebView?, url: String?) {
                binding.progressWebLoading.visibility = View.GONE
                binding.swipeRefreshSpots.isRefreshing = false

                // Inject auth token into web localStorage if paired
                if (prefs.deviceToken.isNotBlank()) {
                    val js = "try { localStorage.setItem('spotnet_device_token', '${prefs.deviceToken}'); } catch(e){}"
                    view?.evaluateJavascript(js, null)
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

    private fun setupNotificationsView() {
        notifAdapter = NotificationsAdapter(
            onNotificationClick = { notif ->
                // Switch to spots tab
                binding.bottomNavigation.selectedItemId = R.id.nav_spots
                if (notif.spots.isNotEmpty()) {
                    val spotId = notif.spots[0].id
                    binding.webViewSpots.loadUrl("${prefs.baseUrl}/#spot-$spotId")
                }
            },
            onMarkReadClick = { notif ->
                lifecycleScope.launch {
                    apiClient.markNotificationRead(notif.id)
                    notif.isRead = true
                    notifAdapter.notifyDataSetChanged()
                    updateNotificationBadge()
                }
            },
            onDeleteClick = { notif ->
                lifecycleScope.launch {
                    apiClient.deleteNotification(notif.id)
                    fetchNotifications()
                }
            }
        )

        binding.rvNotifications.layoutManager = LinearLayoutManager(this)
        binding.rvNotifications.adapter = notifAdapter

        binding.swipeRefreshNotifications.setOnRefreshListener {
            fetchNotifications()
        }

        binding.btnMarkAllRead.setOnClickListener {
            lifecycleScope.launch {
                apiClient.markAllNotificationsRead()
                fetchNotifications()
            }
        }

        binding.btnClearAll.setOnClickListener {
            MaterialAlertDialogBuilder(this)
                .setTitle("Meldingen wissen")
                .setMessage("Weet u zeker dat u alle meldingen wilt verwijderen?")
                .setPositiveButton("Wissen") { _, _ ->
                    lifecycleScope.launch {
                        apiClient.deleteNotification("")
                        fetchNotifications()
                    }
                }
                .setNegativeButton("Annuleren", null)
                .show()
        }
    }

    private fun fetchNotifications() {
        binding.swipeRefreshNotifications.isRefreshing = true
        lifecycleScope.launch {
            val result = apiClient.getNotifications()
            binding.swipeRefreshNotifications.isRefreshing = false

            if (result.isSuccess) {
                val response = result.getOrNull() ?: return@launch
                notifAdapter.setItems(response.notifications)

                binding.layoutEmptyNotifications.visibility =
                    if (response.notifications.isEmpty()) View.VISIBLE else View.GONE
                binding.rvNotifications.visibility =
                    if (response.notifications.isEmpty()) View.GONE else View.VISIBLE

                binding.tvUnreadBadge.text = "Ongelezen meldingen (${response.unreadCount})"
                updateNotificationBadge(response.unreadCount)
            }
        }
    }

    private fun updateNotificationBadge(count: Int = -1) {
        val badge = binding.bottomNavigation.getOrCreateBadge(R.id.nav_notifications)
        if (count > 0) {
            badge.isVisible = true
            badge.number = count
        } else if (count == 0) {
            badge.isVisible = false
        }
    }

    private fun setupSettingsView() {
        binding.tvSettingsHost.text = prefs.baseUrl
        binding.switchNotifications.isChecked = prefs.notificationsEnabled
        binding.switchSound.isChecked = prefs.soundEnabled
        binding.switchVibrate.isChecked = prefs.vibrationEnabled

        binding.switchNotifications.setOnCheckedChangeListener { _, isChecked ->
            prefs.notificationsEnabled = isChecked
            if (isChecked) {
                SpotnetNotificationWorker.schedule(this, prefs.notificationIntervalMinutes)
            } else {
                SpotnetNotificationWorker.cancel(this)
            }
        }

        binding.switchSound.setOnCheckedChangeListener { _, isChecked ->
            prefs.soundEnabled = isChecked
        }

        binding.switchVibrate.setOnCheckedChangeListener { _, isChecked ->
            prefs.vibrationEnabled = isChecked
        }

        binding.btnTestNotification.setOnClickListener {
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

        binding.btnDisconnect.setOnClickListener {
            MaterialAlertDialogBuilder(this)
                .setTitle("Ontkoppelen")
                .setMessage("Weet u zeker dat u dit mobiele apparaat wilt ontkoppelen van Spotnet?")
                .setPositiveButton("Ontkoppelen") { _, _ ->
                    prefs.disconnect()
                    SpotnetNotificationWorker.cancel(this)
                    val intent = Intent(this, ConnectActivity::class.java)
                    intent.putExtra("EXTRA_FORCE_CONNECT", true)
                    startActivity(intent)
                    finish()
                }
                .setNegativeButton("Annuleren", null)
                .show()
        }
    }

    private fun fetchStatus() {
        lifecycleScope.launch {
            val res = apiClient.getStatus()
            if (res.isSuccess) {
                val status = res.getOrNull() ?: return@launch
                binding.tvSettingsStats.text =
                    "Versie: v${status.version} • ${status.totalSpotsInDb} spots in database"
            }
        }
    }
}
