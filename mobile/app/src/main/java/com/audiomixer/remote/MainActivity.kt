package com.audiomixer.remote

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Slider
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent { MixerScreen() }
    }
}

@Composable
private fun MixerScreen() {
    MaterialTheme {
        Surface(
            modifier = Modifier.fillMaxSize(),
            color = Color(0xFF171A22),
            contentColor = Color(0xFFF3F4F8)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .verticalScroll(rememberScrollState())
                    .padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                Text("AudioMixer", style = MaterialTheme.typography.headlineMedium)
                Text("Пульт управления · демонстрационный локальный экран", color = Color(0xFFADB5C7))
                Card(colors = CardDefaults.cardColors(containerColor = Color(0xFF3A3021))) {
                    Text(
                        "Нет подключения к ПК. Регуляторы пока локальные и не меняют звук.",
                        modifier = Modifier.padding(14.dp),
                        color = Color(0xFFFFD38A)
                    )
                }
                Text("MASTER", style = MaterialTheme.typography.titleLarge)
                MixCard("PERSONAL · наушники")
                MixCard("STREAM · OBS")
                Text("КАНАЛЫ", style = MaterialTheme.typography.titleLarge)
                listOf("GAME", "CHAT", "MEDIA", "AUX", "MICROPHONE").forEach { channel ->
                    ChannelCard(channel)
                }
                Spacer(Modifier.height(12.dp))
            }
        }
    }
}

@Composable
private fun MixCard(title: String) {
    val value = remember { mutableFloatStateOf(80f) }
    val muted = remember { mutableStateOf(false) }
    Card(colors = CardDefaults.cardColors(containerColor = Color(0xFF222633))) {
        Column(Modifier.padding(14.dp)) {
            Text(title, style = MaterialTheme.typography.titleMedium)
            Slider(value = value.floatValue, onValueChange = { value.floatValue = it }, valueRange = 0f..100f)
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text("Mute")
                Spacer(Modifier.weight(1f))
                Switch(checked = muted.value, onCheckedChange = { muted.value = it })
            }
        }
    }
}

@Composable
private fun ChannelCard(name: String) {
    val personal = remember { mutableFloatStateOf(75f) }
    val stream = remember { mutableFloatStateOf(90f) }
    val personalMuted = remember { mutableStateOf(false) }
    val streamMuted = remember { mutableStateOf(false) }
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = Color(0xFF222633))
    ) {
        Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(2.dp)) {
            Text(name, style = MaterialTheme.typography.titleMedium)
            Text("Источники и уровни появятся после подключения Host API", color = Color(0xFF929BAD))
            Text("PERSONAL", color = Color(0xFFADB5C7))
            Slider(value = personal.floatValue, onValueChange = { personal.floatValue = it }, valueRange = 0f..100f)
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text("Mute Personal")
                Spacer(Modifier.weight(1f))
                Switch(checked = personalMuted.value, onCheckedChange = { personalMuted.value = it })
            }
            Text("STREAM", color = Color(0xFFADB5C7))
            Slider(value = stream.floatValue, onValueChange = { stream.floatValue = it }, valueRange = 0f..100f)
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text("Mute Stream")
                Spacer(Modifier.weight(1f))
                Switch(checked = streamMuted.value, onCheckedChange = { streamMuted.value = it })
            }
        }
    }
}
