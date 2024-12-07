import React, { useRef } from 'react';
import Breadboard from './components/Breadboard';
import './App.css';
import { Box, Container } from '@mui/material';
import Grid from '@mui/material/Grid2';
import LayoutWidget from './LayoutWidget';
import EditWidget from './EditWidget'

const App = () => {
  const breadboardRef = useRef({});
  const editWidgetRef = useRef();

  const onSaveLayoutHandler = () => {
    console.log("Saving layout: ", breadboardRef);
    breadboardRef.current.saveLayout();
  }

  const openLayout = (event) => {
    console.log("Opening layout: ", event);
    const reader = new FileReader();
    reader.onload = (e) => {
      breadboardRef.current.loadLayout(JSON.parse(e.target.result));
    }
    reader.readAsText(event.target.files[0]);
  }

  return (
    <Grid container spacing={4} columns={20} justifyContent="flex-end"
      style={{
        margin: 0,
        width: '95%',
      }}
    >
      <Grid size={{ xs: 20, sm: 20, md: 16, lg: 16, xl: 16 }}>
        <Container> <Box sx={{
          color: 'white',
          padding: '20px',
          spacing: '20px',
          borderRadius: '8px',
          boxShadow: 5,
        }}>
          <Breadboard ref={breadboardRef} gridRows={5} gridColumns={32} editWidget={editWidgetRef} />
        </Box> </Container>
      </Grid>
      <Grid size={{ xs: 20, sm: 10, md: 4, lg: 4, xl: 4 }}>
        <Box sx={{
          padding: '20px',
          spacing: '20px',
          borderRadius: '8px',
          gap: '10px',
          boxShadow: 5
        }}>
          <LayoutWidget onSaveLayout={onSaveLayoutHandler} openLayout={openLayout} />
          <EditWidget ref={editWidgetRef} />
        </Box>
      </Grid>
    </Grid >
  )
}

export default App;
