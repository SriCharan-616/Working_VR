package com.unity3d.player;

import android.app.Activity;
import android.content.Intent;
import android.speech.RecognizerIntent;
import android.speech.SpeechRecognizer;
import java.util.ArrayList;

public class VoiceBridge {
    
    private static final int SPEECH_REQUEST_CODE = 1001;
    private Activity unityActivity;
    
    public VoiceBridge(Activity activity) {
        this.unityActivity = activity;
    }
    
    public void startSpeechRecognition() {
        Intent intent = new Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH);
        intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM);
        intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE, "en-US");
        intent.putExtra(RecognizerIntent.EXTRA_PROMPT, "Speak now...");
        
        try {
            unityActivity.startActivityForResult(intent, SPEECH_REQUEST_CODE);
        } catch (Exception e) {
            UnityPlayer.UnitySendMessage("VoiceRecognition", "OnSpeechResult", "Error: " + e.getMessage());
        }
    }
    
    public void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (requestCode == SPEECH_REQUEST_CODE && resultCode == Activity.RESULT_OK) {
            ArrayList<String> results = data.getStringArrayListExtra(RecognizerIntent.EXTRA_RESULTS);
            if (results != null && results.size() > 0) {
                String recognizedText = results.get(0);
                UnityPlayer.UnitySendMessage("VoiceRecognition", "OnSpeechResult", recognizedText);
            }
        }
    }
}