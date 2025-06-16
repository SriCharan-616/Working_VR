package com.unity3d.player;

import android.app.Activity;
import android.content.Intent;
import android.speech.RecognizerIntent;
import android.speech.SpeechRecognizer;
import android.speech.RecognitionListener;
import android.util.Log;
import android.os.Handler;
import android.os.Looper;
import java.util.ArrayList;
import java.util.Locale;

public class VoiceBridge implements RecognitionListener {
    private static final String TAG = "VoiceBridge";
    
    private Activity unityActivity;
    private SpeechRecognizer speechRecognizer;
    private Intent speechRecognizerIntent;
    private String gameObjectName;
    private boolean isListening = false;
    private Handler mainHandler;
    
    // Constructor called from Unity
    public VoiceBridge(Activity activity, String objectName) {
        Log.d(TAG, "VoiceBridge constructor called");
        
        this.unityActivity = activity;
        this.gameObjectName = objectName;
        this.mainHandler = new Handler(Looper.getMainLooper());
        
        // Initialize on main thread
        mainHandler.post(new Runnable() {
            @Override
            public void run() {
                initializeSpeechRecognizer();
            }
        });
        
        Log.d(TAG, "VoiceBridge initialization complete");
    }
    
    private void initializeSpeechRecognizer() {
        try {
            Log.d(TAG, "Initializing SpeechRecognizer");
            
            if (SpeechRecognizer.isRecognitionAvailable(unityActivity)) {
                speechRecognizer = SpeechRecognizer.createSpeechRecognizer(unityActivity);
                speechRecognizer.setRecognitionListener(this);
                
                // Setup recognition intent
                speechRecognizerIntent = new Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH);
                speechRecognizerIntent.putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM);
                speechRecognizerIntent.putExtra(RecognizerIntent.EXTRA_LANGUAGE, Locale.getDefault());
                speechRecognizerIntent.putExtra(RecognizerIntent.EXTRA_PARTIAL_RESULTS, false);
                speechRecognizerIntent.putExtra(RecognizerIntent.EXTRA_MAX_RESULTS, 1);
                
                // Set shorter timeout for single letter recognition
                speechRecognizerIntent.putExtra(RecognizerIntent.EXTRA_SPEECH_INPUT_COMPLETE_SILENCE_LENGTH_MILLIS, 1000);
                speechRecognizerIntent.putExtra(RecognizerIntent.EXTRA_SPEECH_INPUT_POSSIBLY_COMPLETE_SILENCE_LENGTH_MILLIS, 1000);
                speechRecognizerIntent.putExtra(RecognizerIntent.EXTRA_SPEECH_INPUT_MINIMUM_LENGTH_MILLIS, 500);
                
                Log.d(TAG, "SpeechRecognizer initialized successfully");
            } else {
                Log.e(TAG, "Speech recognition not available on this device");
                sendErrorToUnity("Speech recognition not available");
            }
            
        } catch (Exception e) {
            Log.e(TAG, "Error initializing SpeechRecognizer: " + e.getMessage());
            sendErrorToUnity("Initialization failed: " + e.getMessage());
        }
    }
    
    // Called from Unity to start listening
    public void startListening() {
        Log.d(TAG, "startListening() called");
        
        mainHandler.post(new Runnable() {
            @Override
            public void run() {
                try {
                    if (speechRecognizer != null && !isListening) {
                        Log.d(TAG, "Starting speech recognition");
                        speechRecognizer.startListening(speechRecognizerIntent);
                        isListening = true;
                        Log.d(TAG, "Speech recognition started successfully");
                    } else {
                        if (speechRecognizer == null) {
                            Log.e(TAG, "SpeechRecognizer is null");
                            sendErrorToUnity("SpeechRecognizer not initialized");
                        } else {
                            Log.w(TAG, "Already listening");
                        }
                    }
                } catch (Exception e) {
                    Log.e(TAG, "Error starting speech recognition: " + e.getMessage());
                    sendErrorToUnity("Failed to start listening: " + e.getMessage());
                    isListening = false;
                }
            }
        });
    }
    
    // Called from Unity to stop listening
    public void stopListening() {
        Log.d(TAG, "stopListening() called");
        
        mainHandler.post(new Runnable() {
            @Override
            public void run() {
                try {
                    if (speechRecognizer != null && isListening) {
                        speechRecognizer.stopListening();
                        Log.d(TAG, "Speech recognition stopped");
                    }
                } catch (Exception e) {
                    Log.e(TAG, "Error stopping speech recognition: " + e.getMessage());
                } finally {
                    isListening = false;
                }
            }
        });
    }
    
    // Called from Unity when destroying the bridge
    public void destroy() {
        Log.d(TAG, "destroy() called");
        
        mainHandler.post(new Runnable() {
            @Override
            public void run() {
                try {
                    if (speechRecognizer != null) {
                        speechRecognizer.cancel();
                        speechRecognizer.destroy();
                        speechRecognizer = null;
                    }
                    isListening = false;
                    Log.d(TAG, "VoiceBridge destroyed successfully");
                } catch (Exception e) {
                    Log.e(TAG, "Error destroying VoiceBridge: " + e.getMessage());
                }
            }
        });
    }
    
    // Recognition Listener Methods
    @Override
    public void onReadyForSpeech(android.os.Bundle params) {
        Log.d(TAG, "onReadyForSpeech");
    }
    
    @Override
    public void onBeginningOfSpeech() {
        Log.d(TAG, "onBeginningOfSpeech");
    }
    
    @Override
    public void onRmsChanged(float rmsdB) {
        // Log.v(TAG, "onRmsChanged: " + rmsdB); // Commented out to reduce log spam
    }
    
    @Override
    public void onBufferReceived(byte[] buffer) {
        Log.d(TAG, "onBufferReceived");
    }
    
    @Override
    public void onEndOfSpeech() {
        Log.d(TAG, "onEndOfSpeech");
    }
    
    @Override
    public void onError(int error) {
        Log.e(TAG, "onError: " + getErrorText(error));
        isListening = false;
        
        String errorMessage = getErrorText(error);
        sendErrorToUnity(errorMessage);
    }
    
    @Override
    public void onResults(android.os.Bundle results) {
        Log.d(TAG, "onResults called");
        isListening = false;
        
        try {
            ArrayList<String> matches = results.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION);
            
            if (matches != null && !matches.isEmpty()) {
                String result = matches.get(0);
                Log.d(TAG, "Recognition result: " + result);
                
                // Process the result to extract single letters
                String processedResult = processSpeechResult(result);
                Log.d(TAG, "Processed result: " + processedResult);
                
                // Send result to Unity
                sendResultToUnity(processedResult);
            } else {
                Log.w(TAG, "No recognition results");
                sendErrorToUnity("No speech detected");
            }
        } catch (Exception e) {
            Log.e(TAG, "Error processing results: " + e.getMessage());
            sendErrorToUnity("Error processing speech: " + e.getMessage());
        }
    }
    
    @Override
    public void onPartialResults(android.os.Bundle partialResults) {
        Log.d(TAG, "onPartialResults");
        // We don't use partial results for letter recognition
    }
    
    @Override
    public void onEvent(int eventType, android.os.Bundle params) {
        Log.d(TAG, "onEvent: " + eventType);
    }
    
    // Helper method to process speech results for single letter recognition
    private String processSpeechResult(String result) {
        if (result == null || result.trim().isEmpty()) {
            return "";
        }
        
        String processed = result.trim().toUpperCase();
        
        // Handle common speech-to-text variations for single letters
        processed = processed.replace("EH", "E");
        processed = processed.replace("AH", "A");
        processed = processed.replace("OH", "O");
        processed = processed.replace("YOU", "U");
        processed = processed.replace("WHY", "Y");
        processed = processed.replace("SEE", "C");
        processed = processed.replace("BEE", "B");
        processed = processed.replace("DEE", "D");
        processed = processed.replace("GEE", "G");
        processed = processed.replace("JAY", "J");
        processed = processed.replace("KAY", "K");
        processed = processed.replace("PEE", "P");
        processed = processed.replace("QUE", "Q");
        processed = processed.replace("ARE", "R");
        processed = processed.replace("ESS", "S");
        processed = processed.replace("TEE", "T");
        processed = processed.replace("VEE", "V");
        processed = processed.replace("DOUBLE YOU", "W");
        processed = processed.replace("EX", "X");
        processed = processed.replace("ZED", "Z");
        processed = processed.replace("ZEE", "Z");
        
        // If result is longer than 1 character, try to extract the first letter
        if (processed.length() > 1) {
            // Check if it's a word that starts with the letter we want
            char firstChar = processed.charAt(0);
            if (Character.isLetter(firstChar)) {
                processed = String.valueOf(firstChar);
            }
        }
        
        Log.d(TAG, "Original: '" + result + "' -> Processed: '" + processed + "'");
        return processed;
    }
    
    // Send recognition result to Unity
    private void sendResultToUnity(String result) {
        try {
            Log.d(TAG, "Sending result to Unity: " + result);
            UnityPlayer.UnitySendMessage(gameObjectName, "OnSpeechResult", result);
        } catch (Exception e) {
            Log.e(TAG, "Error sending result to Unity: " + e.getMessage());
        }
    }
    
    // Send error message to Unity
    private void sendErrorToUnity(String error) {
        try {
            Log.d(TAG, "Sending error to Unity: " + error);
            UnityPlayer.UnitySendMessage(gameObjectName, "OnSpeechError", error);
        } catch (Exception e) {
            Log.e(TAG, "Error sending error to Unity: " + e.getMessage());
        }
    }
    
    // Helper method to convert error codes to readable text
    private String getErrorText(int errorCode) {
        switch (errorCode) {
            case SpeechRecognizer.ERROR_AUDIO:
                return "Audio recording error";
            case SpeechRecognizer.ERROR_CLIENT:
                return "Client side error";
            case SpeechRecognizer.ERROR_INSUFFICIENT_PERMISSIONS:
                return "Insufficient permissions";
            case SpeechRecognizer.ERROR_NETWORK:
                return "Network error";
            case SpeechRecognizer.ERROR_NETWORK_TIMEOUT:
                return "Network timeout";
            case SpeechRecognizer.ERROR_NO_MATCH:
                return "No speech match";
            case SpeechRecognizer.ERROR_RECOGNIZER_BUSY:
                return "Recognition service busy";
            case SpeechRecognizer.ERROR_SERVER:
                return "Server error";
            case SpeechRecognizer.ERROR_SPEECH_TIMEOUT:
                return "No speech input";
            default:
                return "Unknown error: " + errorCode;
        }
    }
    
    // Public method to check if currently listening (for debugging)
    public boolean isListening() {
        return isListening;
    }
}