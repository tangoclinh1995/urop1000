package urop.tnl.phonestation;

import android.content.*;
import android.os.Build;
import android.support.v7.app.ActionBarActivity;
import android.os.Bundle;
import android.util.*;
import android.view.*;
import android.view.View.*;
import android.widget.*;

import java.io.*;
import java.net.*;
import java.util.UUID;

import com.getpebble.android.kit.PebbleKit;
import com.getpebble.android.kit.util.PebbleDictionary;



public class MainActivity extends ActionBarActivity implements OnClickListener {
    final static UUID PEEBLE_APP_UUID = UUID.fromString("10af6f3f-01f3-4dd9-a825-9bb9670dbcf6");

    final static int DEFAULT_CONNECTION_PORT = 12476;
    final static int CONNECTION_ESTABLISH_TIMEOUT = 6000;
    final static int CONNECTION_IO_TIMEOUT = 3000;

    final static String ACKNOWLEDGE_MESSAGE = "PAck";
    final static String CLIENT_DISCONNECTION_MESSAGE = "PDis";
    final static String STRING_MESSAGE_ENCODING = "US-ASCII";

    final static int SENSOR_DATA_KEY = 1;
    final static int FREQUENCY_DATA_KEY = 2;
    final static int COMMAND_DATA_KEY = 3;



    enum ConnectionStatus {
        DISCONNECTED, CONNECTING, CONNECTED, DISCONNECTING
    }



    public static class Globals {
        public static ConnectionStatus clientStatus;
        public static ConnectionStatus serverStatus;

        public static int port = DEFAULT_CONNECTION_PORT;
        public static Socket socket;
        public static OutputStream streamOut;
        public static InputStream streamIn;
    }

    TextView txtPebbleStatus, txtPCSTatus;
    Button btnServer;
    EditText editPort;



    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        txtPebbleStatus = (TextView)findViewById(R.id.txtPebbleStatus);
        txtPCSTatus = (TextView)findViewById(R.id.txtPCStatus);
        btnServer = (Button)findViewById(R.id.btnServer);
        editPort = (EditText)findViewById(R.id.editPort);

        if (PebbleKit.isWatchConnected(getApplicationContext()))
            txtPebbleStatus.setText("Pebble Connected!");
        else
            txtPebbleStatus.setText("Pebble Disconnected!");

        Globals.clientStatus = ConnectionStatus.DISCONNECTED;
        Globals.serverStatus = ConnectionStatus.DISCONNECTED;
        ((TextView)findViewById(R.id.txtSerial)).setText(Build.SERIAL);
        editPort.setText(String.valueOf(DEFAULT_CONNECTION_PORT));

        EstablishPebbleConnections();

        btnServer.setOnClickListener(this);
    }



    @Override
    public void onClick(View v) {
        switch (v.getId()) {
            case R.id.btnServer:
                switch (Globals.serverStatus) {
                    case DISCONNECTED:
                        if (editPort.getText().toString().equals(""))
                            editPort.setText(String.valueOf(DEFAULT_CONNECTION_PORT));

                        Globals.port = Integer.valueOf(editPort.getText().toString());

                        btnServer.setText("Starting...");

                        Thread thread = new Thread(runnStartServer);
                        thread.start();

                        break;
                    case CONNECTED:
                        btnServer.setText("Stopping...");
                        Globals.serverStatus = ConnectionStatus.DISCONNECTING;

                        if (Globals.clientStatus == ConnectionStatus.CONNECTED)
                            Globals.clientStatus = ConnectionStatus.DISCONNECTED;

                        break;
                }

                break;
        }
    }



    void EstablishPebbleConnections() {
        PebbleKit.registerPebbleConnectedReceiver(getApplicationContext(), new BroadcastReceiver() {
            @Override
            public void onReceive(Context context, Intent intent) {
                txtPebbleStatus.post(new Runnable() {
                    @Override
                    public void run() {
                        txtPebbleStatus.setText("Pebble connected");
                    }
                });
            }
        });

        PebbleKit.registerPebbleDisconnectedReceiver(getApplicationContext(), new BroadcastReceiver() {
            @Override
            public void onReceive(Context context, Intent intent) {
                txtPebbleStatus.post(new Runnable() {
                    @Override
                    public void run() {
                        txtPebbleStatus.setText("Pebble disconnected");
                    }
                });
            }
        });

        PebbleKit.registerReceivedDataHandler(this, new PebbleKit.PebbleDataReceiver(PEEBLE_APP_UUID) {
            @Override
            public void receiveData(Context context, int transactionId, PebbleDictionary data) {
                if (Globals.clientStatus == ConnectionStatus.CONNECTED) {
                    Log.i("Bluetooth", "Receiving & Forwarding Data");

                    byte[] forwardData = new byte[0];
                    byte[] tempData;

                    if (data.contains(SENSOR_DATA_KEY)) {
                        tempData = data.getBytes(1);
                        forwardData = new byte[tempData.length + 2];

                        forwardData[0] = 1;
                        forwardData[1] = (byte) (tempData.length / 6);

                        System.arraycopy(tempData, 0, forwardData, 2, tempData.length);
                    } else if (data.contains(FREQUENCY_DATA_KEY))
                        forwardData = new byte[]{2, (byte) (data.getUnsignedIntegerAsLong(2) & 255)};
                    else if (data.contains(COMMAND_DATA_KEY))                      //Command
                        forwardData = new byte[]{3, (byte) (data.getUnsignedIntegerAsLong(3) & 255)};

                    try {
                        Globals.streamOut.write(forwardData);
                        Globals.streamOut.flush();
                    } catch (IOException e) {
                        Log.e("Socket", "Error while sending data to PC!");
                        e.printStackTrace();
                    }

                    PebbleKit.sendAckToPebble(getApplicationContext(), transactionId);
                } else
                    PebbleKit.sendNackToPebble(getApplicationContext(), transactionId);
            }
        });
    }



    void MakeToastThreadSafe(final String message, final int duration) {
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                Toast.makeText(MainActivity.this, message, duration).show();
            }
        });
    }



    void TextViewSetTextThreadSafe(final TextView txtv, final String text) {
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                txtv.setText(text);
            }
        });
    }



    Runnable runnStartServer = new Runnable() {
        @Override
        public void run() {
            ServerSocket server;
            Socket client;

            boolean acknowledgeSuccess;
            byte[] buffer = new byte[4];
            int dataSize;

            Globals.serverStatus = ConnectionStatus.CONNECTING;

            try {
                server = new ServerSocket(Globals.port);
                server.setSoTimeout(CONNECTION_ESTABLISH_TIMEOUT);
            } catch (Exception e) {
                Globals.serverStatus = ConnectionStatus.DISCONNECTED;

                TextViewSetTextThreadSafe(btnServer, "Start");
                MakeToastThreadSafe("Cannot Start Listenning to PC", Toast.LENGTH_LONG);

                Log.e("Server Socket", "Cannot start server socket on port " + Globals.port);
                e.printStackTrace();

                return;
            }

            Globals.serverStatus = ConnectionStatus.CONNECTED;

            TextViewSetTextThreadSafe(btnServer, "Stop");
            MakeToastThreadSafe("Started Listenning to PC", Toast.LENGTH_LONG);

            while (Globals.serverStatus == ConnectionStatus.CONNECTED) {
                Globals.clientStatus = ConnectionStatus.DISCONNECTED;

                try {
                    client = server.accept();
                } catch (Exception e) {
                    Log.e("Server Socket", "Connection timeout");

                    continue;
                }

                if (Globals.serverStatus == ConnectionStatus.DISCONNECTING)
                    break;

                acknowledgeSuccess = false;

                try {
                    client.setSoTimeout(CONNECTION_IO_TIMEOUT);

                    Globals.socket = client;
                    Globals.streamOut = client.getOutputStream();
                    Globals.streamIn = client.getInputStream();

                    //Exchange "PAck" message to verify connection
                    Globals.streamOut.write(ACKNOWLEDGE_MESSAGE.getBytes(STRING_MESSAGE_ENCODING));

                    dataSize = Globals.streamIn.read(buffer, 0, 4);
                    if (dataSize == 4
                            && new String(buffer, STRING_MESSAGE_ENCODING).equals(ACKNOWLEDGE_MESSAGE)) {
                        acknowledgeSuccess = true;
                    } else {
                        Log.e("Server Socket", "PAck message not received!");
                    }
                } catch (Exception e) {
                    Log.e("Server Socket", "Error while establishing connection");
                    e.printStackTrace();
                }

                if (!acknowledgeSuccess)
                    continue;

                Globals.clientStatus = ConnectionStatus.CONNECTED;
                TextViewSetTextThreadSafe(txtPCSTatus, "PC Connected");

                while (Globals.clientStatus == ConnectionStatus.CONNECTED
                        && Globals.serverStatus == ConnectionStatus.CONNECTED) {
                    //Listen to "Disconnection" message from client, if exists
                    try {
                        dataSize = Globals.streamIn.read(buffer, 0, 4);
                        if (dataSize == 4
                                && new String(buffer, STRING_MESSAGE_ENCODING).equals(CLIENT_DISCONNECTION_MESSAGE))
                            Globals.clientStatus = ConnectionStatus.DISCONNECTED;
                    } catch (Exception e) {
                        Log.e("Socket", "Error while receiving message from client");
                        e.printStackTrace();
                    }
                }

                if (Globals.serverStatus == ConnectionStatus.DISCONNECTING) {
                    try {
                        //Send Server "Disconnection" message to client
                        if (Globals.serverStatus == ConnectionStatus.DISCONNECTING)
                            Globals.streamOut.write(new byte[] {4});
                    } catch (Exception e) {
                        Log.e("Socket", "Error while sending server-disconnection message to client");
                    }
                }

                try {
                    client.close();
                } catch (Exception e) {
                    Log.e("Socket", "Error while closing client socket");
                }

                TextViewSetTextThreadSafe(txtPCSTatus, "PC Disconnected");

                if (Globals.serverStatus == ConnectionStatus.DISCONNECTING)
                    break;
            }

            if (Globals.serverStatus == ConnectionStatus.DISCONNECTING) {
                TextViewSetTextThreadSafe(btnServer, "Start");
                Globals.serverStatus = ConnectionStatus.DISCONNECTED;

                try {
                    server.close();
                } catch (Exception e) {
                    Log.e("Server Socket", "Error while closing the server");
                    e.printStackTrace();
                }
            }
        }
    };
}
