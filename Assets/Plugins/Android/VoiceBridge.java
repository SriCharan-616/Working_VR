package com.unity3d.player;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.speech.RecognitionListener;
import android.speech.RecognizerIntent;
import android.speech.SpeechRecognizer;
import android.util.Log;
import java.util.ArrayList;
import java.util.Locale;

public class VoiceBridge {
    private static final String TAG = "VoiceBridge";
    private Activity activity;
    private String gameObjectName;
    private SpeechRecognizer speechRecognizer;
    private Intent speechRecognizerIntent;
    private boolean isListening = false;

    public VoiceBridge(Activity activity, String gameObjectName) {
        this.activity = activity;
        this.gameObjectName = gameObjectName;
        initializeSpeechRecognizer();
    }

    private void initializeSpeechRecognizer() {
        Log.d(TAG, "Initializing speech recognizer");
        
        if (SpeechRecognizer.isRecognitionAvailable(activity)) {
            speechRecognizer = SpeechRecognizer.createSpeechRecognizer(activity);
            speechRecognizer.setRecognitionListener(new RecognitionListener() {
                @Override
                public void onReadyForSpeech(Bundle params) {
                    Log.d(TAG, "Ready for speech");
                }

                @Override
                public void onBeginningOfSpeech() {
                    Log.d(TAG, "Beginning of speech");
                }

                @Override
                public void onRmsChanged(float rmsdB) {
                    // RMS changed - can be used for volume level indication
                }

                @Override
                public void onBufferReceived(byte[] buffer) {
                    // Audio buffer received
                }

                @Override
                public void onEndOfSpeech() {
                    Log.d(TAG, "End of speech");
                }

                @Override
                public void onError(int error) {
                    String errorMessage = getErrorText(error);
                    Log.e(TAG, "Speech recognition error: " + errorMessage);
                    isListening = false;
                    UnityPlayer.UnitySendMessage(gameObjectName, "OnSpeechError", errorMessage);
                }

                @Override
                public void onResults(Bundle results) {
                    Log.d(TAG, "Speech recognition results received");
                    ArrayList<String> matches = results.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION);
                    if (matches != null && !matches.isEmpty()) {
                        String result = matches.get(0);
                        Log.d(TAG, "Recognition result: " + result);
                        UnityPlayer.UnitySendMessage(gameObjectName, "OnSpeechResult", result);
                    }
                    isListening = false;
                }

                @Override
                public void onPartialResults(Bundle partialResults) {
                    // Partial results - can be used for real-time feedback
                }

                @Override
                public void onEvent(int eventType, Bundle params) {
                    // Speech recognition events
                }
            });

            // Set up speech recognizer intent
            speechRecognizerIntent = new Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH);
            speechRecognizerIntent.putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM);
            speechRecognizerIntent.putExtra(RecognizerIntent.EXTRA_LANGUAGE, Locale.getDefault());
            speechRecognizerIntent.putExtra(RecognizerIntent.EXTRA_PARTIAL_RESULTS, true);
            speechRecognizerIntent.putExtra(RecognizerIntent.EXTRA_MAX_RESULTS, 1);
            
            Log.d(TAG, "Speech recognizer initialized successfully");
        } else {
            Log.e(TAG, "Speech recognition not available on this device");
        }
    }

    public void startListening() {
        Log.d(TAG, "Start listening called");
        
        if (speechRecognizer != null && !isListening) {
            activity.runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    try {
                        speechRecognizer.startListening(speechRecognizerIntent);
                        isListening = true;
                        Log.d(TAG, "Speech recognition started");
                    } catch (Exception e) {
                        Log.e(TAG, "Error starting speech recognition: " + e.getMessage());
                        UnityPlayer.UnitySendMessage(gameObjectName, "OnSpeechError", "Failed to start listening: " + e.getMessage());
                    }
                }
            });
        } else {
            if (speechRecognizer == null) {
                Log.e(TAG, "Speech recognizer is null");
                UnityPlayer.UnitySendMessage(gameObjectName, "OnSpeechError", "Speech recognizer not initialized");
            }
            if (isListening) {
                Log.w(TAG, "Already listening");
            }
        }
    }

    public void stopListening() {
        Log.d(TAG, "Stop listening called");
        
        if (speechRecognizer != null && isListening) {
            activity.runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    try {
                        speechRecognizer.stopListening();
                        isListening = false;
                        Log.d(TAG, "Speech recognition stopped");
                    } catch (Exception e) {
                        Log.e(TAG, "Error stopping speech recognition: " + e.getMessage());
                    }
                }
            });
        }
    }

    public void destroy() {
        Log.d(TAG, "Destroying VoiceBridge");
        
        if (speechRecognizer != null) {
            activity.runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    try {
                        if (isListening) {
                            speechRecognizer.stopListening();
                        }
                        speechRecognizer.destroy();
                        speechRecognizer = null;
                        isListening = false;
                        Log.d(TAG, "Speech recognizer destroyed");
                    } catch (Exception e) {
                        Log.e(TAG, "Error destroying speech recognizer: " + e.getMessage());
                    }
                }
            });
        }
    }

    private String getErrorText(int errorCode) {
        String message;
        switch (errorCode) {
            case SpeechRecognizer.ERROR_AUDIO:
                message = "Audio recording error";
                break;
            case SpeechRecognizer.ERROR_CLIENT:
                message = "Client side error";
                break;
            case SpeechRecognizer.ERROR_INSUFFICIENT_PERMISSIONS:
                message = "Insufficient permissions";
                break;
            case SpeechRecognizer.ERROR_NETWORK:
                message = "Network error";
                break;
            case SpeechRecognizer.ERROR_NETWORK_TIMEOUT:
                message = "Network timeout";
                break;
            case SpeechRecognizer.ERROR_NO_MATCH:
                message = "No match";
                break;
            case SpeechRecognizer.ERROR_RECOGNIZER_BUSY:
                message = "RecognitionService busy";
                break;
            case SpeechRecognizer.ERROR_SERVER:
                message = "Error from server";
                break;
            case SpeechRecognizer.ERROR_SPEECH_TIMEOUT:
                message = "No speech input";
                break;
            default:
                message = "Didn't understand, please try again.";
                break;
        }
        return message;
    }
}