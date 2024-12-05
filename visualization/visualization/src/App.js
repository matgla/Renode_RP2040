import React, { useRef } from 'react';
import Breadboard from './components/Breadboard';
import './App.css';
import { Typography, Button, Box } from '@mui/material';
import Grid from '@mui/material/Grid2';
import { CloudUpload, Save } from '@mui/icons-material';
import { styled } from '@mui/material/styles';

const VisuallyHiddenInput = styled('input')({
  clip: 'rect(0 0 0 0)',
  clipPath: 'inset(50%)',
  height: 1,
  overflow: 'hidden',
  position: 'absolute',
  bottom: 0,
  left: 0,
  whiteSpace: 'nowrap',
  width: 1,
});

const App = () => {
  const breadboardRef = useRef({});

  const onSaveLayout = () => {
    console.log("Saving layout: ", breadboardRef);
    breadboardRef.current.saveLayout();
  }

  const openLayout = (event) => {
    console.log("Opening layout: ", event);
    console.log(event.target.files[0]);
    const reader = new FileReader();
    reader.onload = (e) => {
      console.log(e.target.result);
    }
    reader.readAsText(event.target.files[0]);
  }

  return (
    <Grid container spacing={4} columns={20}
      style={{
        margin: 0,
        width: '95%'
      }}
    >
      <Grid size={{ xs: 20, sm: 16 }}>
        <Box sx={{
          color: 'white',
          padding: '20px',
          borderRadius: '8px',
          boxShadow: 3,
        }}>
          <Breadboard ref={breadboardRef} gridRows={5} gridColumns={32} />
        </Box>
      </Grid>
      <Grid size={4}>
        <div>
          <Box sx={{
            padding: '20px',
            borderRadius: '8px',
            gap: '10px',
            boxShadow: 3,
          }}>
            <Grid container columns={8} spacing={2}>
              <Grid size={8}>
                <Typography align="center" variant="h5" gutterBottom sx={{
                  color: 'black'
                }}> Layout </Typography>
              </Grid>
              <Grid size={4}>
                <Button 
                  variant="contained" 
                  onClick={onSaveLayout}
                  startIcon={<Save />}
                >Save</Button>
              </Grid>
                <Grid size={4}>

               <Button
                  component="label"
                  variant="contained"
                  tabIndex={-1}
                  startIcon={<CloudUpload />}
                >
                  Load
                  <VisuallyHiddenInput
                    type="file"
                    onChange={openLayout}
                    multiple
                  />
                </Button>
              
                </Grid>
              </Grid>
          </Box>
        </div>
      </Grid>
    </Grid>
  )
}

export default App;
