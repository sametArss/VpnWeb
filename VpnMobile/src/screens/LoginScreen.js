import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  StyleSheet,
  Alert,
  SafeAreaView,
  KeyboardAvoidingView,
  Platform,
  ActivityIndicator
} from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { FontAwesome5, MaterialCommunityIcons } from '@expo/vector-icons';
import api from '../api/api';
import VpnTunnelService from '../services/VpnTunnelService';

const LoginScreen = ({ onLoginSuccess }) => {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [isVpnActive, setIsVpnActive] = useState(false);

  useEffect(() => {
    const checkVpnStatus = async () => {
      try {
        const active = await VpnTunnelService.isActive();
        setIsVpnActive(active);
      } catch (e) {
        console.error(e);
      }
    };
    checkVpnStatus();
    
    // Periyodik kontrol yapalım (VPN kapatılırsa arayüz güncellensin)
    const interval = setInterval(checkVpnStatus, 2000);
    return () => clearInterval(interval);
  }, []);

  const handleForceDisconnect = async () => {
    setLoading(true);
    try {
      await VpnTunnelService.stop();
      await AsyncStorage.removeItem('active_vpn_connection');
      setIsVpnActive(false);
      Alert.alert('Başarılı', 'Aktif VPN tüneli kapatıldı. İnternetiniz geri gelmiş olmalı.');
    } catch (e) {
      Alert.alert('Hata', 'VPN kapatılamadı.');
    } finally {
      setLoading(false);
    }
  };

  const handleLogin = async () => {
    if (!email || !password) {
      Alert.alert('Hata', 'Lütfen tüm alanları doldurun.');
      return;
    }

    setLoading(true);
    try {
      const response = await api.post('/auth/login', { email, password });
      const { token } = response.data;

      await AsyncStorage.setItem('token', token);
      onLoginSuccess();
    } catch (error) {
      console.error(error);
      const message = error.response?.data?.message || 'Giriş başarısız. Lütfen bilgilerinizi kontrol edin.';
      const errorDetail = error.message ? `\n\n(Hata Detayı: ${error.message})` : '';
      Alert.alert('Hata', message + errorDetail);
    } finally {
      setLoading(false);
    }
  };

  return (
    <SafeAreaView style={styles.container}>
      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
        style={styles.content}
      >
        <View style={styles.statusDotContainer}>
          <View style={styles.pulseDot} />
          <Text style={styles.statusText}>System Operational</Text>
        </View>

        <View style={styles.header}>
          <View style={styles.logoContainer}>
            <MaterialCommunityIcons name="shield-half-full" size={60} color="#3b82f6" />
          </View>
          <Text style={styles.title}>GlobalShield</Text>
          <Text style={styles.subtitle}>Secure . Anonymous . Private</Text>
        </View>

        {isVpnActive && (
          <TouchableOpacity style={styles.emergencyBanner} onPress={handleForceDisconnect}>
            <MaterialCommunityIcons name="power" size={20} color="#fff" />
            <Text style={styles.emergencyBannerText}>
              Aktif VPN Kapat (İnternet Yoksa Tıkla)
            </Text>
          </TouchableOpacity>
        )}

        <View style={styles.form}>
          <View style={styles.inputGroup}>
            <Text style={styles.label}>Email Address</Text>
            <View style={styles.inputWrapper}>
              <MaterialCommunityIcons name="email-outline" size={20} color="rgba(255,255,255,0.4)" style={styles.inputIcon} />
              <TextInput
                style={styles.input}
                placeholder="name@example.com"
                placeholderTextColor="rgba(255,255,255,0.3)"
                value={email}
                onChangeText={setEmail}
                autoCapitalize="none"
                keyboardType="email-address"
              />
            </View>
          </View>

          <View style={styles.inputGroup}>
            <Text style={styles.label}>Password</Text>
            <View style={styles.inputWrapper}>
              <MaterialCommunityIcons name="lock-outline" size={20} color="rgba(255,255,255,0.4)" style={styles.inputIcon} />
              <TextInput
                style={styles.input}
                placeholder="••••••••"
                placeholderTextColor="rgba(255,255,255,0.3)"
                value={password}
                onChangeText={setPassword}
                secureTextEntry={!showPassword}
              />
              <TouchableOpacity onPress={() => setShowPassword(!showPassword)}>
                <MaterialCommunityIcons
                  name={showPassword ? "eye-off-outline" : "eye-outline"}
                  size={20}
                  color="rgba(255,255,255,0.4)"
                />
              </TouchableOpacity>
            </View>
          </View>

          <TouchableOpacity
            style={[styles.loginButton, loading && styles.disabledButton]}
            onPress={handleLogin}
            disabled={loading}
          >
            {loading ? (
              <ActivityIndicator color="#fff" />
            ) : (
              <>
                <Text style={styles.loginButtonText}>Secure Login</Text>
                <MaterialCommunityIcons name="arrow-right" size={20} color="#fff" style={{ marginLeft: 10 }} />
              </>
            )}
          </TouchableOpacity>

          <View style={styles.footer}>
            <Text style={styles.footerText}>Don't have an account? </Text>
            <TouchableOpacity>
              <Text style={styles.registerLink}>Get Protected Now</Text>
            </TouchableOpacity>
          </View>
        </View>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0f172a', // Web'deki karanlık tema
  },
  content: {
    flex: 1,
    padding: 30,
    justifyContent: 'center',
  },
  statusDotContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    position: 'absolute',
    top: 60,
    alignSelf: 'center',
    backgroundColor: 'rgba(255,255,255,0.05)',
    paddingVertical: 6,
    paddingHorizontal: 12,
    borderRadius: 20,
  },
  pulseDot: {
    width: 8,
    height: 8,
    borderRadius: 4,
    backgroundColor: '#4ade80',
    marginRight: 8,
  },
  statusText: {
    color: '#4ade80',
    fontSize: 12,
    fontWeight: '600',
  },
  header: {
    alignItems: 'center',
    marginBottom: 40,
  },
  logoContainer: {
    marginBottom: 15,
  },
  title: {
    fontSize: 32,
    fontWeight: 'bold',
    color: '#fff',
    letterSpacing: 1,
  },
  subtitle: {
    fontSize: 14,
    color: 'rgba(255,255,255,0.5)',
    marginTop: 5,
    letterSpacing: 2,
  },
  emergencyBanner: {
    backgroundColor: '#ef4444',
    paddingVertical: 12,
    paddingHorizontal: 15,
    borderRadius: 12,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 20,
    elevation: 3,
    shadowColor: '#ef4444',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.3,
    shadowRadius: 4
  },
  emergencyBannerText: {
    color: '#fff',
    fontWeight: 'bold',
    marginLeft: 8,
    fontSize: 13
  },
  form: {
    width: '100%',
  },
  inputGroup: {
    marginBottom: 20,
  },
  label: {
    color: 'rgba(255,255,255,0.8)',
    marginBottom: 8,
    fontSize: 14,
    fontWeight: '500',
  },
  inputWrapper: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(255,255,255,0.05)',
    borderRadius: 12,
    paddingHorizontal: 15,
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.1)',
    height: 55,
  },
  inputIcon: {
    marginRight: 12,
  },
  input: {
    flex: 1,
    color: '#fff',
    fontSize: 16,
  },
  loginButton: {
    backgroundColor: '#3b82f6',
    height: 55,
    borderRadius: 12,
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    marginTop: 10,
    elevation: 5,
    shadowColor: '#3b82f6',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.3,
    shadowRadius: 8,
  },
  disabledButton: {
    opacity: 0.7,
  },
  loginButtonText: {
    color: '#fff',
    fontSize: 16,
    fontWeight: 'bold',
  },
  footer: {
    flexDirection: 'row',
    justifyContent: 'center',
    marginTop: 25,
  },
  footerText: {
    color: 'rgba(255,255,255,0.5)',
  },
  registerLink: {
    color: '#3b82f6',
    fontWeight: 'bold',
  },
});

export default LoginScreen;
